using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrossGenV.Classes;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;

namespace ProjectWardle
{
    public class WardleOptions
    {
        #region Configurable options

        /// <summary>
        /// Mapping of map prefixes (BIOA_STA70, etc) to their destination names
        /// </summary>
        public List<WardleLevel> levels;

        /// <summary>
        /// If lightmaps and shadowmaps should be stripped and dynamic lighting turned on
        /// </summary>
        public bool useDynamicLighting = true;

        /// <summary>
        /// Strips shadow maps off. If using dynamic lighting, shadow maps are always stripped
        /// </summary>
        public bool stripShadowMaps = false;

        /// <summary>
        /// If light and shadowmaps for meshes ported from ME1 (not using LE1 donor) should be ported instead of stripped. This may not look good but may be possible to adjust.
        /// </summary>
        public bool allowTryingPortedMeshLightMap = true;

        /// <summary>
        /// If terrains should have their lightmaps ported over (if they exist)
        /// </summary>
        public bool portTerrainLightmaps = true;

        /// <summary>
        /// If level models should be ported.
        /// </summary>
        public bool portModels = false;

        /// <summary>
        /// If the audio localizations should be ported
        /// </summary>
        public bool portAudioLocalizations = true;

        /// <summary>
        /// If a level's list of StreamableTextureInstance's should be copied over.
        /// </summary>
        public bool installTexturesInstanceMap = true;

        /// <summary>
        /// If a level's list of textures to force streaming should be copied over.
        /// </summary>
        public bool installForceTextureStreaming = false;

        /// <summary>
        /// If debug features should be enabled in the build
        /// </summary>
        public bool debugBuild = false;

        /// <summary>
        /// If static lighting should be converted to non-static lighting. Only works if debugBuild is true
        /// </summary>
        public bool debugConvertStaticLightingToNonStatic = false;

        /// <summary>
        /// If each actor porting should also import into a new asset package that can speed up build 
        /// </summary>
        public bool debugBuildAssetCachePackage = false;

        /// <summary>
        /// The cache that is passed through to sub operations. You can change the
        /// CacheMaxSize to tune memory usage vs performance.
        /// </summary>
        public PackageCache cache = new PackageCache() { CacheMaxSize = 20 };
        #endregion

        #region Autoset options - Do not change these
        public IMEPackage vTestHelperPackage;
        public ObjectInstanceDB objectDB;
        public IMEPackage assetCachePackage;
        internal int assetCacheIndex;
        #endregion

        /// <summary>
        /// Delegate to invoke when a status message should be presented to the user
        /// </summary>
        public Action<string> SetStatusText { get; init; }

        /// <summary>
        /// Game to run for
        /// </summary>
        public MEGame game { get; set; }

        /// <summary>
        /// Directory to scan for files from
        /// </summary>
        public string sourceFileDir { get; set; }

        public bool generateLevelStart { get; set; }
    }

    public class WardleLevel
    {
        /// <summary>
        /// Adds a start location at this position in the BioP
        /// </summary>
        public Point3D? StartLocation { get; set; }

        /// <summary>
        /// Sets the KillZ on the BioP file
        /// </summary>
        public int KillZ { get; set; }

        /// <summary>
        /// Input directory containing the files for the levels
        /// </summary>
        public string LevelDirectory { get; set; }
    }
}
