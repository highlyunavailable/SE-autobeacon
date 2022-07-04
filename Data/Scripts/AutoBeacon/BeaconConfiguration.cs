using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Serialization;
using VRage.Utils;
using VRageMath;

namespace AutoBeacon
{
    public class BeaconConfiguration
    {
        private const string ConfigFileName = "AutoRangeBeaconConfig.xml";

        private const double DefaultMaxBlockMass = 5000000;
        private const double DefaultMaxWeaponPCU = 200000;
        private const float DefaultMinBeaconRadius = 500;
        private const float DefaultMaxBeaconRadius = 10000;
        private const float DefaultSmallGridRangeFactor = 0.2f;
        private const double DefaultForceRescanPeriodSecs = 15;
        private const bool DefaultOverrideHUDText = true;
        private const float DefaultCooldownSecs = 60f;
        private static readonly Vector3I DefaultBigGrid = new Vector3I(45, 25, 15);
        private static readonly string DefaultOverrideFallbackName = "Notice Me Senpai! >_<;";

        // Tiers/modifiers:
        // Presence of lightning advances each level to the next level.
        // Positive numbers reduce the broadcast range. Negative numbers increase it.
        // Values are applied as (1-value)
        // 0.1: Nuisance
        // 0.2: Light Weather
        // 0.3: Heavy Weather or Light Weather with lightning
        // 0.4: Heavy Weather with lightning
        // 0.5: Severe Weather or extremely high levels of electromagnetic interference 

        private static Dictionary<string, float> DefaultAffectingWeatherTypes = new Dictionary<string, float>
        {
            { "AlienFogHeavy", 0.3f },
            { "AlienFogLight", 0.2f },
            { "AlienRainHeavy", 0.3f },
            { "AlienRainLight", 0.2f },
            { "AlienThunderstormHeavy", 0.4f },
            { "AlienThunderstormLight", 0.3f },
            { "Dust", 0.1f },
            { "ElectricStorm", 0.5f },
            { "FogHeavy", 0.3f },
            { "FogLight", 0.2f },
            { "MarsSnow", 0.1f },
            { "MarsStormHeavy", 0.4f },
            { "MarsStormLight", 0.2f },
            { "RainHeavy", 0.3f },
            { "RainLight", 0.2f },
            { "SandStormHeavy", 0.3f },
            { "SandStormLight", 0.2f },
            { "SnowHeavy", 0.3f },
            { "SnowLight", 0.2f },
            { "ThunderstormHeavy", 0.4f },
            { "ThunderstormLight", 0.3f },
            // Kharak specific
            { "RadiationStorm", 0.5f }
        };

        /// <summary>
        /// Smallest beacon range possible after modifications
        /// </summary>
        public float MinBeaconRadius { get; set; }

        /// <summary>
        /// Largest beacon range possible after modifications
        /// </summary>
        public float MaxBeaconRadius { get; set; }

        /// <summary>
        /// Maximum PCU in weapons for range calculations (can be less than server maximum to make this max out early)
        /// </summary>
        public double MaxWeaponPCU { get; set; }

        /// <summary>
        /// Maximum grid mass for range calculations
        /// </summary>
        public double MaxRangeBlockMass { get; set; }

        /// <summary>
        /// The maximum grid size for range calculations.
        /// </summary>
        public Vector3I MaxGridDimensions { get; set; }

        /// <summary>
        /// How much of a reduction small grids get to their beacon range
        /// </summary>
        public float SmallGridRangeFactor { get; set; }

        /// <summary>
        /// What weather types affect the beacon range, and the modifiers for those weathers. Unlisted = no effect
        /// </summary>
        public SerializableDictionary<string, float> AffectingWeatherTypes { get; set; }

        /// <summary>
        /// Scan the grid and all connected grids every ForceRescanPeriodSecs seconds
        /// </summary>
        public double ForceRescanPeriodSecs { get; set; }

        /// <summary>
        /// Should override HUD text on the beacon or not with the grid name and beacon radius (unmodified)
        /// </summary>
        public bool OverrideHUDText { get; set; }

        /// <summary>
        /// The HUD name set when the grid has a predefined name like "Large Grid 1234"
        /// </summary>
        public string OverrideFallbackName { get; set; }

        /// <summary>
        /// Approximately how long it will take to go from full speed visibility to minimum speed visibility
        /// </summary>
        public float CooldownSecs { get; set; }

        public static BeaconConfiguration LoadSettings()
        {
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(ConfigFileName, typeof(BeaconConfiguration)))
            {
                try
                {
                    BeaconConfiguration loadedSettings;
                    using (var reader =
                           MyAPIGateway.Utilities.ReadFileInWorldStorage(ConfigFileName, typeof(BeaconConfiguration)))
                    {
                        loadedSettings =
                            MyAPIGateway.Utilities.SerializeFromXML<BeaconConfiguration>(reader.ReadToEnd());
                    }

                    if (loadedSettings == null || !loadedSettings.Validate())
                    {
                        throw new Exception("Invalid beacon configuration");
                    }

                    SaveSettings(loadedSettings);
                    return loadedSettings;
                }
                catch (Exception e)
                {
                    MyLog.Default.WriteLineAndConsole(
                        $"Failed to load Auto Range Beacon settings: {e.Message}\n{e.StackTrace}");
                }

                MyAPIGateway.Utilities.WriteBinaryFileInWorldStorage(ConfigFileName + ".old",
                    typeof(BeaconConfiguration));
            }

            var settings = new BeaconConfiguration();
            settings.SetDefaults();
            SaveSettings(settings);
            return settings;
        }

        private static void SaveSettings(BeaconConfiguration settings)
        {
            try
            {
                using (var writer =
                       MyAPIGateway.Utilities.WriteFileInWorldStorage(ConfigFileName, typeof(BeaconConfiguration)))
                {
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole(
                    $"Failed to save Auto Range Beacon settings: {e.Message}\n{e.StackTrace}");
            }
        }

        private bool Validate()
        {
            if (MaxRangeBlockMass <= 0)
            {
                MaxRangeBlockMass = DefaultMaxBlockMass;
            }

            if (MaxWeaponPCU <= 0)
            {
                MaxWeaponPCU = DefaultMaxWeaponPCU;
            }

            if (MaxBeaconRadius <= 0)
            {
                MaxBeaconRadius = DefaultMaxBeaconRadius;
            }

            if (MinBeaconRadius <= 0)
            {
                MinBeaconRadius = DefaultMinBeaconRadius;
            }

            if (SmallGridRangeFactor <= 0)
            {
                SmallGridRangeFactor = DefaultSmallGridRangeFactor;
            }

            if (ForceRescanPeriodSecs <= 0)
            {
                ForceRescanPeriodSecs = DefaultForceRescanPeriodSecs;
            }

            if (CooldownSecs <= 0)
            {
                CooldownSecs = DefaultCooldownSecs;
            }

            if (AffectingWeatherTypes == null)
            {
                AffectingWeatherTypes = new SerializableDictionary<string, float>
                    { Dictionary = new Dictionary<string, float>(DefaultAffectingWeatherTypes) };
            }

            if (OverrideHUDText && string.IsNullOrWhiteSpace(OverrideFallbackName))
            {
                OverrideFallbackName = DefaultOverrideFallbackName;
            }

            if (MinBeaconRadius > MaxBeaconRadius)
            {
                return false;
            }

            return true;
        }

        private void SetDefaults()
        {
            MaxRangeBlockMass = DefaultMaxBlockMass;
            MaxWeaponPCU = DefaultMaxWeaponPCU;
            MaxBeaconRadius = DefaultMaxBeaconRadius;
            MinBeaconRadius = DefaultMinBeaconRadius;
            SmallGridRangeFactor = DefaultSmallGridRangeFactor;
            MaxGridDimensions = DefaultBigGrid;
            ForceRescanPeriodSecs = DefaultForceRescanPeriodSecs;
            AffectingWeatherTypes = new SerializableDictionary<string, float>
                { Dictionary = new Dictionary<string, float>(DefaultAffectingWeatherTypes) };
            OverrideHUDText = DefaultOverrideHUDText;
            OverrideFallbackName = DefaultOverrideFallbackName;
            CooldownSecs = DefaultCooldownSecs;
        }
    }
}