using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CrossGenV.Classes;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace ProjectWardle
{
    internal class OneToOne
    {
        public static void OneToOneFilePort(WardleOptions wardleOptions, string inputFile, string destDir, WardleLevel levelSet, List<string> levelFiles)
        {
            var packageBaseName = Path.GetFileNameWithoutExtension(inputFile);
            var isBioP = packageBaseName.StartsWith(@"BioP_");

            // The current level set file
            var destPackagePath = Path.Combine(destDir, $"{packageBaseName}.pcc");
            MEPackageHandler.CreateEmptyLevel(destPackagePath, wardleOptions.game);
            var destPackage = MEPackageHandler.OpenMEPackage(destPackagePath);
            var destTheWorld = destPackage.FindExport(@"TheWorld.PersistentLevel");

            // Build each file set
            Console.WriteLine($"Opening {inputFile}");
            var sourcePackage = MEPackageHandler.OpenMEPackage(inputFile);

            // Not needed right now
            //Precorrect(sourcePackage);

            var theWorld = ObjectBinary.From<Level>(sourcePackage.FindExport(@"TheWorld.PersistentLevel"));
            var levelObjects = theWorld.Actors.Where(x => x > 0).Select(x => sourcePackage.GetUExport(x)).ToList();

            var model = levelObjects.FirstOrDefault(x => x.ClassName == @"Model");

            if (model != null && levelObjects.Count(x => x.ClassName == @"Model") == 1)
            {
                model.indexValue = 4; // Match the dest package
            }
            else
            {
                Console.WriteLine(@"HAVE TO FIX MODEL MANUALLY");
            }

            var staticMeshes = sourcePackage.Exports.Where(x =>
                    x.ClassName is @"StaticMeshActor"
                     or @"SkeletalMeshActor"

                     // LE3 ONLY
                     or @"BlockingVolume"
                     or @"InterpActor"
                     or @"HeightFog"
                     or @"LensFlareSource"
                     or @"DecalActor"
                     //or @"Emitter"
                     or @"StaticLightCollectionActor"
                     or @"StaticMeshCollectionActor")
                .ToList();

            foreach (var sm in staticMeshes)
            {
                // Ensure it's unique to the dest

                Console.WriteLine($"\tPorting {sm.InstancedFullPath}");
                if (destPackage.FindExport(sm.InstancedFullPath) != null)
                {
                    Console.WriteLine($@" > Already exists: {sm.InstancedFullPath}");
                }
                else
                {
                    var badLinks = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sm, destPackage, destTheWorld, true, new RelinkerOptionsPackage() { IsCrossGame = true, TargetGameDonorDB = wardleOptions.objectDB, Cache = wardleOptions.cache }, out var _);
                }
            }

            if (isBioP && levelSet.StartLocation != null)
            {
                // Add a BioPlayerStart node so this file can be booted
                var bsl = wardleOptions.vTestHelperPackage.FindExport(@"BioStartLocation_0");
                var badLinks = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, bsl, destPackage, destTheWorld, true, new RelinkerOptionsPackage(), out var startLoc);

                PathEdUtils.SetLocation(startLoc as ExportEntry, levelSet.StartLocation);
            }

            

            if (isBioP)
            {
                // Add the list of levels
                var otherLevels = levelFiles.Where(x => x != inputFile).ToList();

                int lskIndex = 1;
                foreach (var ol in otherLevels)
                {
                    var itemToClone = sourcePackage.Exports.FirstOrDefault(x => x.ClassName == @"LevelStreamingKismet");
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, itemToClone, destPackage, destTheWorld, true, new RelinkerOptionsPackage(), out var portedLSK);
                    portedLSK.indexValue += 1 + lskIndex++;
                    (portedLSK as ExportEntry).WriteProperty(new NameProperty(Path.GetFileNameWithoutExtension(ol), "PackageName"));
                }
                Program.RebuildStreamingLevels(destPackage);

                // Port over the BioTriggerStream
                var bts = sourcePackage.Exports.FirstOrDefault(x => x.ClassName == @"BioTriggerStream");
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, bts, destPackage, destTheWorld, true, new RelinkerOptionsPackage(), out var portedBts);

                // Set the KillZ
                if (levelSet.KillZ != 0)
                {
                    destPackage.FindExport(@"TheWorld.PersistentLevel.BioWorldInfo_2").WriteProperty(new FloatProperty(levelSet.KillZ, @"KillZ"));
                }
            }

            Program.RebuildPersistentLevelChildren(destTheWorld, wardleOptions);
            destPackage.Save();

            var brokenMaterials = ShaderCacheManipulator.GetBrokenMaterials(destPackage);
            foreach (var v in brokenMaterials)
            {
                Console.WriteLine($@"Material has no shader in game: {v.InstancedFullPath}");
            }
        }
    }
}