using System.Diagnostics;
using System.Text.RegularExpressions;
using CrossGenV.Classes;
using LegendaryExplorerCore;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;

namespace ProjectWardle
{
    /// <summary>
    /// Entrypoint for the ProjectWardle
    /// </summary>
    public static class Program
    {
        internal static void IndexFileForObjDB(ObjectInstanceDB objectDB, MEGame game, IMEPackage package)
        {
            // Index package path
            int packageNameIndex;
            if (package.FilePath.StartsWith(MEDirectories.GetDefaultGamePath(game)))
            {
                // Get relative path
                packageNameIndex = objectDB.GetNameTableIndex(package.FilePath.Substring(MEDirectories.GetDefaultGamePath(game).Length + 1));
            }
            else
            {
                // Store full path
                packageNameIndex = objectDB.GetNameTableIndex(package.FilePath);
            }

            // Index objects
            foreach (var exp in package.Exports)
            {
                var ifp = exp.InstancedFullPath;

                // Things to ignore
                if (ifp.StartsWith(@"TheWorld"))
                    continue;
                if (ifp.StartsWith(@"ObjectReferencer"))
                    continue;

                // Index it
                objectDB.AddRecord(ifp, packageNameIndex, true);
            }
        }

        public static void Main(string[] args)
        {
            Console.WriteLine("Project Wardle by ME3Tweaks");

            // Initialize Legendary Explorer Core
            LegendaryExplorerCoreLib.InitLib(TaskScheduler.Current, x => Console.WriteLine($"ERROR: {x}"));

            WardleOptions wardleOptions = new WardleOptions()
            {
                game = MEGame.LE3,
                debugBuild = true,
                cache = new PackageCache(),

                // MP
                sourceFileDir = @"Z:\Mass Effect 3 Builds\WARDLE",

                // Wardle
                //sourceFileDir = @"B:\SteamLibrary\steamapps\common\Mass Effect Legendary Edition\Game\ME1\BioGame\CookedPCConsole",

                generateLevelStart = true,
                levels = new()
                {
                    // ME3 
                    new WardleLevel()
                    {
                        //MatchPattern = @"BioA_MPMoon*",
                        //DestFilenameBase = @"BioA_MP1_MoonME3",
                        LevelDirectory = @"Z:\Mass Effect 3 Builds\WARDLE\MPMoon",
                        StartLocation = new Point3D()
                        {
                            X=-15131,
                            Y=-4366,
                            Z=-1208,
                        },
                        KillZ = -3200
                    },
                    //{@"BioA_MPHosp", @"BioA_MP3_HospME3"}

                    // LE1
                    // NOVERIA
                    //{@"BIOA_ICE20", @"BioA_NoveriaLE1_AlpineCity"},
                    //{@"BIOA_ICE50", @"BioA_NoveriaLE1_Peak15"},
                    //{@"BIOA_ICE60", @"BioA_NoveriaLE1_Hotlabs"},

                    // FREIGHTERS
                    //{@"BIOA_FRE31", @"BioA_MSVCornucopiaLE1"},
                    //{@"BIOA_FRE32", @"BioA_MSVOntarioLE1"},
                    //{@"BIOA_FRE33", @"BioA_MSVWorthingtonLE1"},
                    //{@"BIOA_FRE34", @"BioA_MSVFedeleLE1"},
                    //{@"BIOA_FRE35", @"BioA_DepotSigma23LE1"},

                    // CITADEL
                    /*
                    {@"BIOA_STA20", @"BioA_CitHubLE1_Presidium"},
                    {@"BIOA_STA30", @"BioA_CitHubLE1_CSec"},
                    {@"BIOA_STA60", @"BioA_CitHubLE1_Wards"},
                    {@"BIOA_STA70", @"BioA_CitHubLE1_Tower"},
                    */
                }
            };

            Console.WriteLine(@"Loading Object DB");

#if __LE3__
            wardleOptions.objectDB = ObjectInstanceDB.DeserializeDB(File.ReadAllText(@"C:\Users\mgame\AppData\Roaming\LegendaryExplorer\ObjectDatabases\LE3.json"));
            var donorPath = @"X:\Google Drive\Mass Effect Legendary Modding\LE3\ProjectWardle\LE3\Donors";
#else
            wardleOptions.objectDB = ObjectInstanceDB.DeserializeDB(File.ReadAllText(@"C:\Users\mgame\AppData\Roaming\LegendaryExplorer\ObjectDatabases\LE2.json"));
            var donorPath = @"X:\Google Drive\Mass Effect Legendary Modding\LE3\ProjectWardle\LE2\Donors";
#endif

            wardleOptions.objectDB.BuildLookupTable();

            // Add extra donors and VTestHelper package
            foreach (var file in Directory.GetFiles(donorPath))
            {
                if (file.RepresentsPackageFilePath())
                {
                    if (Path.GetFileNameWithoutExtension(file) == "VTestHelper")
                    {
                        // Load the VTestHelper, don't index it
                        Console.WriteLine($@"Inventorying VTestHelper");
                        wardleOptions.vTestHelperPackage = MEPackageHandler.OpenMEPackage(file, forceLoadFromDisk: true); // Do not put into cache

                        // Inventory the classes from vtest helper to ensure they can be created without having to be in the 
                        // code for LEC
                        foreach (var e in wardleOptions.vTestHelperPackage.Exports.Where(x => x.IsClass && x.InheritsFrom("SequenceObject")))
                        {
                            var classInfo = GlobalUnrealObjectInfo.generateClassInfo(e);
                            var defaults = wardleOptions.vTestHelperPackage.GetUExport(ObjectBinary.From<UClass>(e).Defaults);
                            wardleOptions.SetStatusText($@"  Inventorying class {e.InstancedFullPath}");
                            GlobalUnrealObjectInfo.GenerateSequenceObjectInfoForClassDefaults(defaults);
                            GlobalUnrealObjectInfo.InstallCustomClassInfo(e.ObjectName, classInfo, e.Game);
                        }
                    }
                    else
                    {
                        // Inventory
                        Console.WriteLine($@"Inventorying {Path.GetFileName(file)}");
                        using var p = MEPackageHandler.OpenMEPackage(file);
                        IndexFileForObjDB(wardleOptions.objectDB, wardleOptions.game, p);
                    }
                }
            }


            // Port and build files
            PortWardle(wardleOptions);

            foreach (var v in EntryImporter.NonDonorItems.OrderBy(x => x))
            {
                Console.WriteLine(v);
            }

            //if (installAndBootGame)
            //{
            var mmPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\ME3Tweaks", "ExecutableLocation", null);
            if (mmPath != null && File.Exists(mmPath))
            {

#if __LE3__
                var moddesc = Path.Combine(@"Y:\ModLibrary\LE3\Project Wardle", "moddesc.ini");
#else
                var moddesc = Path.Combine(@"Y:\ModLibrary\LE2\Project Wardle LE2", "moddesc.ini");
#endif
                if (File.Exists(moddesc))
                {
                    Console.WriteLine("Installing Project Wardle and running game, check ME3Tweaks Mod Manager");
                    ProcessStartInfo psi = new ProcessStartInfo(mmPath, $"--installmod \"{moddesc}\" --bootgame {wardleOptions.game}");
                    Process.Start(psi);
                }
            }
            //}
        }

        private static void PortWardle(WardleOptions wardleOptions)
        {
            //var sourceLE1Files = @"X:\Google Drive\Mass Effect Legendary Modding\LE3\ProjectWardle\LE1Files";
            var sourceLE1FileDir = wardleOptions.sourceFileDir;

            string destDir = "";

            if (wardleOptions.game == MEGame.LE3)
            {
                destDir = @"Y:\ModLibrary\LE3\Project Wardle\DLC_MOD_ProjectWardle\CookedPCConsole";
            }
            else if (wardleOptions.game == MEGame.LE2)
            {
                destDir = @"Y:\ModLibrary\LE2\Project Wardle LE2\DLC_MOD_ProjectWardleLE2\CookedPCConsole";
            }

            // The list of files to actually operate on
            var sourceFiles = Directory.GetFiles(sourceLE1FileDir).Where(x => x.RepresentsPackageFilePath()).ToList();

            // Build each level set
            foreach (var levelSet in wardleOptions.levels)
            {
                var levelFiles = Directory.GetFiles(levelSet.LevelDirectory).Where(x => x.RepresentsPackageFilePath()).ToList();
                foreach (var pcc in levelFiles)
                {
                    OneToOne.OneToOneFilePort(wardleOptions, pcc, destDir, levelSet, levelFiles);
                }
            }
        }

        private static void EnsureUniqueObjectNames(IMEPackage destPackage, List<ExportEntry> staticMeshes)
        {

        }

        public static void RebuildStreamingLevels(IMEPackage Pcc)
        {
            try
            {
                var levelStreamingKismets = new List<ExportEntry>();
                ExportEntry bioworldinfo = null;
                foreach (ExportEntry exp in Pcc.Exports)
                {
                    switch (exp.ClassName)
                    {
                        case "BioWorldInfo" when exp.ObjectName == "BioWorldInfo":
                            bioworldinfo = exp;
                            continue;
                        case "LevelStreamingKismet" when exp.ObjectName == "LevelStreamingKismet":
                            levelStreamingKismets.Add(exp);
                            continue;
                    }
                }

                levelStreamingKismets = levelStreamingKismets
                    .OrderBy(o => o.GetProperty<NameProperty>("PackageName").ToString()).ToList();
                if (bioworldinfo != null)
                {
                    var streamingLevelsProp =
                        bioworldinfo.GetProperty<ArrayProperty<ObjectProperty>>("StreamingLevels") ??
                        new ArrayProperty<ObjectProperty>("StreamingLevels");

                    streamingLevelsProp.Clear();
                    foreach (ExportEntry exp in levelStreamingKismets)
                    {
                        streamingLevelsProp.Add(new ObjectProperty(exp.UIndex));
                    }

                    bioworldinfo.WriteProperty(streamingLevelsProp);
                }
                else
                {
                    Console.WriteLine("No BioWorldInfo object found in this file.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error setting streaming levels:\n" + ex.Message);
            }
        }


        private static void Precorrect(IMEPackage sourcePackage)
        {
            // Make lightmaps unique
            var packageTextures = sourcePackage.Exports.Where(x => x.ClassName is "LightMapTexture2D" or "ShadowMapTexture2D").ToList();
            var prefix = Path.GetFileNameWithoutExtension(sourcePackage.FilePath);
            foreach (var texture in packageTextures)
            {
                texture.ObjectName = new NameReference($"{prefix}_{texture.ObjectName}", texture.ObjectName.Number);
            }

            // Everything should point to the same model
            sourcePackage.Exports.FirstOrDefault(x => x.ClassName == @"Model").indexValue = 4;
        }

        public static void RebuildPersistentLevelChildren(ExportEntry pl, WardleOptions vTestOptions)
        {
            ExportEntry[] actorsToAdd = pl.FileRef.Exports.Where(exp => exp.Parent == pl && exp.IsA("Actor")).ToArray();
            Level level = ObjectBinary.From<Level>(pl);
            level.Actors.Clear();
            foreach (var actor in actorsToAdd)
            {
                if (vTestOptions != null && (!vTestOptions.debugBuild || !vTestOptions.debugConvertStaticLightingToNonStatic))
                {
                    // Don't add things that are in collection actors. 
                    // In a debug build we want to not use them in a collection actor
                    // so that they are not static.
                    var lc = actor.GetProperty<ObjectProperty>("LightComponent");
                    if (lc != null && pl.FileRef.TryGetUExport(lc.Value, out var lightComp))
                    {
                        if (lightComp.Parent != null && lightComp.Parent.ClassName == "StaticLightCollectionActor")
                            continue; // don't add this one
                    }
                }
                else if (vTestOptions != null && vTestOptions.debugBuild && vTestOptions.debugConvertStaticLightingToNonStatic && actor.ClassName == "StaticLightCollectionActor")
                {
                    continue; // Debug builds with debugConvertStaticLightingToNonStatic don't add StaticLightCollectionActor, instead porting over the lights individually.
                }

                level.Actors.Add(new UIndex(actor.UIndex));
            }

            //if (level.Actors.Count > 1)
            //{

            // BioWorldInfo will always be present
            // or at least, it better be!
            // Slot 2 has to be blank in LE. In ME1 i guess it was a brush.
            level.Actors.Insert(1, new UIndex(0)); // This is stupid
                                                   //}

            pl.WriteBinary(level);
        }
    }
}