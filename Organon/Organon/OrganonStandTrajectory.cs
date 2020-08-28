﻿using Osu.Cof.Ferm.Heuristics;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Osu.Cof.Ferm.Organon
{
    public class OrganonStandTrajectory : StandTrajectory
    {
        private FiaVolume fiaVolume;
        private Dictionary<FiaCode, float[]> organonCalibration;
        private OrganonGrowth organonGrowth;

        public OrganonConfiguration Configuration { get; private set; }
        public OrganonStandDensity[] DensityByPeriod { get; private set; }

        public Heuristic Heuristic { get; set; }
        public OrganonStand[] StandByPeriod { get; private set; }

        public OrganonStandTrajectory(OrganonStand stand, OrganonConfiguration organonConfiguration, TimberValue timberValue, int lastPlanningPeriod)
            : base(timberValue, lastPlanningPeriod, organonConfiguration.Treatments.Harvests.Count == 1 ? organonConfiguration.Treatments.Harvests[0].Period : 0)
        {
            if (organonConfiguration.Treatments.Harvests.Count > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(organonConfiguration));
            }
            if (timberValue == null)
            {
                throw new ArgumentNullException(nameof(timberValue));
            }

            int maximumPlanningPeriodIndex = lastPlanningPeriod + 1;
            this.Configuration = new OrganonConfiguration(organonConfiguration);
            this.DensityByPeriod = new OrganonStandDensity[maximumPlanningPeriodIndex];
            this.fiaVolume = new FiaVolume();
            this.organonCalibration = organonConfiguration.CreateSpeciesCalibration();
            this.organonGrowth = new OrganonGrowth();

            this.Heuristic = null;
            this.Name = stand.Name;
            this.PeriodLengthInYears = organonConfiguration.Variant.TimeStepInYears;
            this.PeriodZeroAgeInYears = stand.AgeInYears;
            this.StandByPeriod = new OrganonStand[maximumPlanningPeriodIndex];

            this.DensityByPeriod[0] = new OrganonStandDensity(stand, organonConfiguration.Variant);
            foreach (Trees treesOfSpecies in stand.TreesBySpecies.Values)
            {
                this.IndividualTreeSelectionBySpecies.Add(treesOfSpecies.Species, new int[treesOfSpecies.Capacity]);
            }
            this.StandByPeriod[0] = new OrganonStand(stand); // subsequent periods initialized lazily in Simulate()
            this.StandByPeriod[0].Name += 0;

            this.GetVolumeAndValue(0);
        }

        // shallow copy FIA and Organon for now
        // deep copy of tree growth data
        public OrganonStandTrajectory(OrganonStandTrajectory other)
            : base(other)
        {
            this.fiaVolume = other.fiaVolume;
            this.organonCalibration = other.organonCalibration;
            this.Configuration = new OrganonConfiguration(other.Configuration);
            this.organonGrowth = other.organonGrowth;

            this.DensityByPeriod = new OrganonStandDensity[other.PlanningPeriods];
            this.Heuristic = other.Heuristic;
            this.StandByPeriod = new OrganonStand[other.PlanningPeriods];

            for (int periodIndex = 0; periodIndex < this.PlanningPeriods; ++periodIndex)
            {
                OrganonStandDensity otherDensity = other.DensityByPeriod[periodIndex];
                if (otherDensity != null)
                {
                    this.DensityByPeriod[periodIndex] = new OrganonStandDensity(otherDensity);
                }

                OrganonStand otherStand = other.StandByPeriod[periodIndex];
                if (otherStand != null)
                {
                    this.StandByPeriod[periodIndex] = new OrganonStand(otherStand);
                }
            }
        }

        public void CopyFrom(OrganonStandTrajectory other)
        {
            this.CopySelectionsFrom(other);

            // for now, shallow copies where feasible
            this.fiaVolume = other.fiaVolume; // has no state
            this.Heuristic = other.Heuristic; // assumed invariant within OptimizeCmdlet.Run() tasks
            this.organonCalibration = other.organonCalibration; // unused
            this.organonGrowth = other.organonGrowth; // BUGBUG: has no state, should have run state which can be copied

            // deep copies of mutable state changed by modified tree selection and resimulation
            for (int periodIndex = 0; periodIndex < this.StandByPeriod.Length; ++periodIndex)
            {
                OrganonStandDensity otherDensity = other.DensityByPeriod[periodIndex];
                if (otherDensity != null)
                {
                    if (this.DensityByPeriod[periodIndex] == null)
                    {
                        this.DensityByPeriod[periodIndex] = new OrganonStandDensity(otherDensity);
                    }
                    else
                    {
                        this.DensityByPeriod[periodIndex].CopyFrom(otherDensity);
                    }
                }

                // may need deep copy of treatment because 
                // 1) thinning prescriptions are being evaluated and therefore the best prescription needs to be reported
                // 2) BUGBUG: no Organon run state object has been implemented
                this.Configuration.CopyFrom(other.Configuration);

                OrganonStand otherStand = other.StandByPeriod[periodIndex];
                if (otherStand != null)
                {
                    if (this.StandByPeriod[periodIndex] == null)
                    {
                        this.StandByPeriod[periodIndex] = new OrganonStand(otherStand);
                    }
                    else
                    {
                        this.StandByPeriod[periodIndex].CopyTreeGrowthFrom(otherStand);
                    }
                }
                else
                {
                    this.StandByPeriod[periodIndex] = null;
                }
            }
        }

        public void CopySelectionsFrom(StandTrajectory other)
        {
            Debug.Assert(Object.ReferenceEquals(this, other) == false);

            if ((this.HarvestPeriods != other.HarvestPeriods) || (this.PlanningPeriods != other.PlanningPeriods))
            {
                // TODO: check rest of stand properties
                throw new ArgumentOutOfRangeException(nameof(other));
            }

            Array.Copy(other.BasalAreaRemoved, 0, this.BasalAreaRemoved, 0, this.BasalAreaRemoved.Length);
            this.ThinningVolume.CopyFrom(other.ThinningVolume);
            this.StandingVolume.CopyFrom(other.StandingVolume);

            foreach (KeyValuePair<FiaCode, int[]> otherSelectionForSpecies in other.IndividualTreeSelectionBySpecies)
            {
                int[] thisSelectionForSpecies = this.IndividualTreeSelectionBySpecies[otherSelectionForSpecies.Key];
                if (otherSelectionForSpecies.Value.Length != thisSelectionForSpecies.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(other.IndividualTreeSelectionBySpecies));
                }
                Array.Copy(otherSelectionForSpecies.Value, 0, thisSelectionForSpecies, 0, thisSelectionForSpecies.Length);
            }
            this.TreeSelectionChangedSinceLastSimulation = other.TreeSelectionChangedSinceLastSimulation;
        }

        private void GetVolumeAndValue(int periodIndex)
        {
            // this.GetFiaVolumeAndValue(periodIndex);
            this.GetTaperVolumeAndValue(periodIndex);
        }

        //private void GetFiaVolumeAndValue(int periodIndex)
        //{
        //    // harvest volumes, if applicable
        //    foreach (IHarvest harvest in this.Configuration.Treatments.Harvests)
        //    {
        //        if (harvest.Period == periodIndex)
        //        {
        //            // tree's expansion factor is set to zero when it's marked for harvest
        //            // Use tree's volume at end of the the previous period.
        //            // TODO: track per species volumes
        //            OrganonStand previousStand = this.StandByPeriod[periodIndex - 1];
        //            double harvestedCvts4perAcre = 0.0F;
        //            double harvestedScribner6x32footLogPerAcre = 0.0F;
        //            foreach (Trees previousTreesOfSpecies in previousStand.TreesBySpecies.Values)
        //            {
        //                if (previousTreesOfSpecies.Units != Units.English)
        //                {
        //                    throw new NotSupportedException();
        //                }

        //                int[] individualTreeSelection = this.IndividualTreeSelectionBySpecies[previousTreesOfSpecies.Species];
        //                Debug.Assert(individualTreeSelection.Length == previousTreesOfSpecies.Capacity);
        //                for (int treeIndex = 0; treeIndex < previousTreesOfSpecies.Count; ++treeIndex)
        //                {
        //                    if (individualTreeSelection[treeIndex] == periodIndex)
        //                    {
        //                        float treesPerAcre = previousTreesOfSpecies.LiveExpansionFactor[treeIndex];
        //                        Debug.Assert(treesPerAcre > 0.0F);
        //                        harvestedCvts4perAcre += treesPerAcre * this.fiaVolume.GetMerchantableCubicFeet(previousTreesOfSpecies, treeIndex);
        //                        harvestedScribner6x32footLogPerAcre += treesPerAcre * this.fiaVolume.GetScribnerBoardFeet(previousTreesOfSpecies, treeIndex);
        //                    }
        //                }
        //            }

        //            this.ThinningVolume.Cubic[periodIndex] = (float)(Constant.AcresPerHectare * Constant.CubicMetersPerCubicFoot * harvestedCvts4perAcre);
        //            this.ThinningVolume.Scribner[periodIndex] = (float)(0.001 * Constant.AcresPerHectare * harvestedScribner6x32footLogPerAcre);
        //            if (harvestedCvts4perAcre == 0.0F)
        //            {
        //                Debug.Assert(harvestedScribner6x32footLogPerAcre == 0.0F);
        //                this.ThinningVolume.NetPresentValue[periodIndex] = 0.0F;
        //            }
        //            else
        //            {
        //                int thinAge = this.GetStartOfPeriodAge(periodIndex);
        //                this.ThinningVolume.NetPresentValue[periodIndex] = this.TimberValue.GetNetPresentValueThiningScribner(this.ThinningVolume.Scribner[periodIndex], thinAge);
        //            }

        //            // could make more specific by checking if harvest removes at least one tree
        //            Debug.Assert((this.BasalAreaRemoved[periodIndex] > 0.0F && this.ThinningVolume.Cubic[periodIndex] > 0.0F && this.ThinningVolume.Scribner[periodIndex] > 0.0F) ||
        //                         (this.BasalAreaRemoved[periodIndex] == 0.0F && this.ThinningVolume.Cubic[periodIndex] == 0.0F && this.ThinningVolume.Scribner[periodIndex] == 0.0F));
        //        }
        //    }

        //    // standing volume
        //    OrganonStand stand = this.StandByPeriod[periodIndex];
        //    double standingCvts4perAcre = 0.0F;
        //    double standingScribner6x32footLogPerAcre = 0.0F;
        //    foreach (Trees treesOfSpecies in stand.TreesBySpecies.Values)
        //    {
        //        if (treesOfSpecies.Units != Units.English)
        //        {
        //            throw new NotSupportedException();
        //        }

        //        int[] individualTreeSelection = this.IndividualTreeSelectionBySpecies[treesOfSpecies.Species];
        //        for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
        //        {
        //            if ((individualTreeSelection[treeIndex] == 0) || (periodIndex < individualTreeSelection[treeIndex]))
        //            {
        //                float treesPerAcre = treesOfSpecies.LiveExpansionFactor[treeIndex];
        //                standingCvts4perAcre += treesPerAcre * this.fiaVolume.GetMerchantableCubicFeet(treesOfSpecies, treeIndex);
        //            }
        //        }
        //        standingScribner6x32footLogPerAcre += this.fiaVolume.GetScribnerBoardFeetPerAcre(treesOfSpecies);
        //    }

        //    this.StandingVolume.Cubic[periodIndex] = (float)(Constant.AcresPerHectare * Constant.CubicMetersPerCubicFoot * standingCvts4perAcre);
        //    this.StandingVolume.Scribner[periodIndex] = (float)(0.001 * Constant.AcresPerHectare * standingScribner6x32footLogPerAcre);
        //    int harvestAge = this.GetEndOfPeriodAge(periodIndex);
        //    this.StandingVolume.NetPresentValue[periodIndex] = this.TimberValue.GetNetPresentValueRegenerationHarvestScribner(this.StandingVolume.Scribner[periodIndex], harvestAge);
        //}

        private void GetTaperVolumeAndValue(int periodIndex)
        {
            // harvest volumes, if applicable
            foreach (IHarvest harvest in this.Configuration.Treatments.Harvests)
            {
                if (harvest.Period == periodIndex)
                {
                    // tree's expansion factor is set to zero when it's marked for harvest
                    // Use tree's volume at end of the the previous period.
                    // TODO: track per species volumes
                    OrganonStand previousStand = this.StandByPeriod[periodIndex - 1];
                    double harvestedCubicMetersPerAcre = 0.0F;
                    double harvestedScribnerPerAcre = 0.0F;
                    double harvestedValuePerAcre = 0.0F;
                    foreach (Trees previousTreesOfSpecies in previousStand.TreesBySpecies.Values)
                    {
                        Debug.Assert(previousTreesOfSpecies.Units == Units.English, "TODO: per hectare.");

                        int[] individualTreeSelection = this.IndividualTreeSelectionBySpecies[previousTreesOfSpecies.Species];
                        this.TimberValue.ScaledVolumeThinning.GetVolume(previousTreesOfSpecies, individualTreeSelection, out double merchantableCubicVolume, out double scribnerVolume, out double value);
                        harvestedCubicMetersPerAcre += merchantableCubicVolume;
                        harvestedScribnerPerAcre += scribnerVolume;
                        harvestedValuePerAcre += value;
                    }

                    this.ThinningVolume.Cubic[periodIndex] = (float)(Constant.AcresPerHectare * Constant.Bucking.DefectAndBreakageReduction * harvestedCubicMetersPerAcre);
                    this.ThinningVolume.Scribner[periodIndex] = (float)(0.001 * Constant.AcresPerHectare * Constant.Bucking.DefectAndBreakageReduction * harvestedScribnerPerAcre);
                    if (harvestedCubicMetersPerAcre == 0.0F)
                    {
                        Debug.Assert(harvestedScribnerPerAcre == 0.0F);
                        this.ThinningVolume.NetPresentValue[periodIndex] = 0.0F;
                    }
                    else
                    {
                        int thinAge = this.GetStartOfPeriodAge(periodIndex);
                        float harvestNpvGraded = Constant.AcresPerHectare * Constant.Bucking.DefectAndBreakageReduction * this.TimberValue.ToNetPresentValue((float)harvestedValuePerAcre, thinAge);
                        // float harvestNpvSimple = this.TimberValue.GetNetPresentValueThiningScribner(this.ThinningVolume.Scribner[periodIndex], thinAge);
                        this.ThinningVolume.NetPresentValue[periodIndex] = harvestNpvGraded;
                    }

                    // could make more specific by checking if harvest removes at least one tree
                    Debug.Assert((this.BasalAreaRemoved[periodIndex] > 0.0F && this.ThinningVolume.Cubic[periodIndex] > 0.0F && this.ThinningVolume.Scribner[periodIndex] > 0.0F) ||
                                 (this.BasalAreaRemoved[periodIndex] == 0.0F && this.ThinningVolume.Cubic[periodIndex] == 0.0F && this.ThinningVolume.Scribner[periodIndex] == 0.0F));
                }
            }

            // standing volume
            OrganonStand stand = this.StandByPeriod[periodIndex];
            double standingCubicMetersPerAcre = 0.0F;
            double standingScribnerPerAcre = 0.0F;
            double standingValuePerAcre = 0.0F;
            foreach (Trees treesOfSpecies in stand.TreesBySpecies.Values)
            {
                Debug.Assert(treesOfSpecies.Units == Units.English, "TODO: per hectare.");

                this.TimberValue.ScaledVolumeRegenerationHarvest.GetVolume(treesOfSpecies, out double merchantableCubicVolume, out double scribnerVolume, out double value);
                standingCubicMetersPerAcre += merchantableCubicVolume;
                standingScribnerPerAcre += scribnerVolume;
                standingValuePerAcre += value;
            }

            this.StandingVolume.Cubic[periodIndex] = (float)(Constant.AcresPerHectare * Constant.Bucking.DefectAndBreakageReduction * standingCubicMetersPerAcre);
            this.StandingVolume.Scribner[periodIndex] = (float)(0.001 * Constant.AcresPerHectare * Constant.Bucking.DefectAndBreakageReduction * standingScribnerPerAcre);
            int harvestAge = this.GetEndOfPeriodAge(periodIndex);
            float npvGraded = Constant.AcresPerHectare * Constant.Bucking.DefectAndBreakageReduction * this.TimberValue.ToNetPresentValue((float)standingValuePerAcre, harvestAge);
            // float npvSimple = this.TimberValue.GetNetPresentValueRegenerationHarvestScribner(this.StandingVolume.Scribner[periodIndex], harvestAge);
            this.StandingVolume.NetPresentValue[periodIndex] = npvGraded;
        }

        public void Simulate()
        {
            // TODO: clear volumes and/or basal area?
            this.Configuration.Treatments.ClearHarvestState();

            // period 0 is the initial condition and therefore never needs to be simulated
            // Since simulation is computationally expensive, the current implementation is lazy and relies on triggers to simulate only on demand. In 
            // particular, in single entry cases no stand modification occurs before the target harvest period and, therefore, periods 1...entry - 1 need
            // to be simulated only once.
            Debug.Assert(this.StandByPeriod.Length > 1, "At least one simulation period expected.");
            bool standEnteredOrNotSimulated = this.StandByPeriod[1] == null; // not yet simulated case, entry checked in loop below
            float[] crownCompetitionByHeight = null;
            OrganonStand simulationStand = standEnteredOrNotSimulated ? new OrganonStand(this.StandByPeriod[0]) : null;
            for (int periodIndex = 1; periodIndex < this.PlanningPeriods; ++periodIndex)
            {
                OrganonStandDensity standDensity = this.DensityByPeriod[periodIndex - 1];

                // trigger stand resimulation due to change in tree selection
                if (this.Configuration.Treatments.IsTriggerInPeriod(periodIndex))
                {
                    float basalAreaRemoved = this.Configuration.Treatments.EvaluateTriggers(periodIndex, this);
                    if (simulationStand == null)
                    {
                        simulationStand = new OrganonStand(this.StandByPeriod[periodIndex - 1]);
                    }
                    foreach (KeyValuePair<FiaCode, int[]> individualTreeSelection in this.IndividualTreeSelectionBySpecies)
                    {
                        for (int treeIndex = 0; treeIndex < individualTreeSelection.Value.Length; ++treeIndex) // assumes trailing capacity is set to zero and of insignificant length
                        {
                            // if needed, this loop can be changed to use either the simulation stand's tree count or a reference tree count rather than capacity
                            if (individualTreeSelection.Value[treeIndex] == periodIndex)
                            {
                                simulationStand.TreesBySpecies[individualTreeSelection.Key].LiveExpansionFactor[treeIndex] = 0.0F;
                            }
                        }
                    }

                    this.BasalAreaRemoved[periodIndex] = basalAreaRemoved;
                    if (this.TreeSelectionChangedSinceLastSimulation)
                    {
                        crownCompetitionByHeight = OrganonStandDensity.GetCrownCompetitionByHeight(this.Configuration.Variant, simulationStand);
                        standDensity = new OrganonStandDensity(simulationStand, this.Configuration.Variant);
                    }
                    standEnteredOrNotSimulated = true;
                }

                if (standEnteredOrNotSimulated)
                {
                    // simulate this period
                    if (crownCompetitionByHeight == null)
                    {
                        crownCompetitionByHeight = OrganonStandDensity.GetCrownCompetitionByHeight(this.Configuration.Variant, simulationStand);
                    }
                    this.organonGrowth.Grow(periodIndex, this.Configuration, simulationStand, standDensity, this.organonCalibration, 
                                            ref crownCompetitionByHeight, out OrganonStandDensity standDensityAfterGrowth, out int _);

                    this.DensityByPeriod[periodIndex] = standDensityAfterGrowth;
                    if (this.StandByPeriod[periodIndex] == null)
                    {
                        // lazy initialization
                        OrganonStand standForPeriod = new OrganonStand(simulationStand);
                        standForPeriod.Name = standForPeriod.Name[0..^1] + periodIndex;
                        this.StandByPeriod[periodIndex] = standForPeriod;
                    }
                    else
                    {
                        // update on resimulation
                        this.StandByPeriod[periodIndex].CopyTreeGrowthFrom(simulationStand);
                    }

                    // recalculate volume for this period
                    this.GetVolumeAndValue(periodIndex);

                    #if DEBUG
                    if (periodIndex < this.ThinningVolume.Scribner.Length)
                    {
                        Debug.Assert((this.BasalAreaRemoved[periodIndex] == 0.0F && this.ThinningVolume.Scribner[periodIndex] == 0.0F) || (this.BasalAreaRemoved[periodIndex] > 0.0F && this.ThinningVolume.Scribner[periodIndex] > 0.0F));
                    }
                    #endif
                }
            }

            this.TreeSelectionChangedSinceLastSimulation = false;
        }
    }
}
