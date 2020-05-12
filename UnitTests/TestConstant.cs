﻿using Osu.Cof.Ferm.Organon;
using System.Collections.ObjectModel;

namespace Osu.Cof.Ferm.Test
{
    internal static class TestConstant
    {
        public const float AcresPerHectare = 2.47105F;
        public const int DbhQuantiles = 5;
        public const float FeetPerMeter = 3.28084F;
        public const float InchesPerCm = 0.393701F;
        public const float MetersPerInch = 0.0254F;
        public const float SquareMetersPerSquareFoot = 0.092903F;

        public static ReadOnlyCollection<FiaCode> TreeSpeciesList = new ReadOnlyCollection<FiaCode>(new FiaCode[] {
                FiaCode.AbiesGrandis,
                FiaCode.CalocedrusDecurrens,
                FiaCode.PinusPonderosa,
                FiaCode.Salix,

                FiaCode.PinusLambertiana,
                FiaCode.TaxusBrevifolia,
                FiaCode.TsugaHeterophylla,
                FiaCode.AcerMacrophyllum,
                FiaCode.AlnusRubra,
                FiaCode.ArbutusMenziesii,
                FiaCode.ChrysolepisChrysophyllaVarChrysophylla,
                FiaCode.CornusNuttallii,
                FiaCode.QuercusGarryana,

                FiaCode.AbiesConcolor,
                FiaCode.NotholithocarpusDensiflorus,
                FiaCode.PinusLambertiana,
                FiaCode.QuercusChrysolepis,
                FiaCode.QuercusKelloggii
            });

        public static readonly OrganonVariant[] Variants = new OrganonVariant[] 
        { 
            new OrganonVariantNwo(),
            new OrganonVariantSwo(),
            new OrganonVariantSmc(),
            new OrganonVariantRap()
        };

        public static class Default
        {
            public const float CrownRatio = 0.5F;
            public const float HarvestProbability = 0.5F;
            public const float MaximumReinekeStandDensityIndex = 600.0F;
            public const int SimulationCyclesToRun = 20; // 20 5 year time steps
            public const float SiteIndex = 125.0F; // feet at 50 years
        }

        public static class Maximum
        {
            // TODO: make DBH, height, and crown limits species specific
            public const float CrownCompetitionFactor = 1000.0F;
            // for want of a better option, allow trees larger than the ~100 cm upper bound (<65 cm preferred) of Poudel 2018's dataset
            public const float DiameterInInches = 60.0F;
            public const float ExpansionFactor = 100.0F;
            public const float HeightInFeet = 380.0F; // SESE Hyperion
            public const float LargestCrownWidthInFeet = 300.0F;
            public const float TreeBasalAreaLarger = 1000.0F;
            public const float MaximumCrownWidthInFeet = 300.0F;
            public const int StandAgeInYears = 500;
            public const float StandCrownCompetitionFactor = 10000.0F;
        }
    }
}
