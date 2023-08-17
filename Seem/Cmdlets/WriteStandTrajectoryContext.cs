﻿using Mars.Seem.Optimization;
using Mars.Seem.Silviculture;
using Mars.Seem.Tree;
using System;

namespace Mars.Seem.Cmdlets
{
    public class WriteStandTrajectoryContext
    {
        // global settings invariant across all stands
        public float DiameterClassSize { get; private init; }
        public FinancialScenarios FinancialScenarios { get; private init; }
        public bool HarvestsOnly { get; private init; }
        public float MaximumDiameter { get; private init; }
        public bool NoCarbon { get; private init; }
        public bool NoEquipmentProductivity { get; private init; }
        public bool NoFinancial { get; private init; }
        public bool NoHarvestCosts { get; private init; }
        public bool NoTimberSorts { get; private init; }
        public bool NoTreeGrowth { get; private init; }
        public int? StartYear { get; init; }

        // per stand settings
        public int EndOfRotationPeriodIndex { get; set; }
        public int FinancialIndex { get; set; }
        public string LinePrefix { get; set; }

        public WriteStandTrajectoryContext(FinancialScenarios financialScenarios, bool harvestsOnly, bool noTreeGrowth, bool noFinancial, bool noCarbon, bool noHarvestCosts, bool noTimberSorts, bool noEquipmentProductivity, float diameterClassSize, float maximumDiameter)
        {
            this.DiameterClassSize = diameterClassSize;
            this.FinancialScenarios = financialScenarios;
            this.HarvestsOnly = harvestsOnly;
            this.MaximumDiameter = maximumDiameter;
            this.NoCarbon = noCarbon;
            this.NoEquipmentProductivity = noEquipmentProductivity;
            this.NoFinancial = noFinancial;
            this.NoHarvestCosts = noHarvestCosts;
            this.NoTimberSorts = noTimberSorts;
            this.NoTreeGrowth = noTreeGrowth;
            this.StartYear = null;

            this.EndOfRotationPeriodIndex = -1;
            this.FinancialIndex = -1;
            this.LinePrefix = String.Empty;
        }

        public int GetPeriodsToWrite(StandTrajectory trajectory)
        {
            if (this.HarvestsOnly)
            {
                int harvests = 0;
                for (int harvestIndex = 0; harvestIndex < trajectory.Treatments.Harvests.Count; ++harvestIndex)
                {
                    ++harvests;

                    Harvest harvest = trajectory.Treatments.Harvests[harvestIndex];
                    if (harvest.Period == this.EndOfRotationPeriodIndex)
                    {
                        return harvests; // thin scheduled in same period as end of rotation
                    }
                }

                return ++harvests; // add one for regeneration harvest
            }

            return trajectory.StandByPeriod.Length;
        }
    }
}
