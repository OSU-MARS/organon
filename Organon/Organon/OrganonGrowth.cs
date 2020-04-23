﻿using Osu.Cof.Ferm.Species;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Osu.Cof.Ferm.Organon
{
    internal class OrganonGrowth
    {
        public static float GetCrownRatioAdjustment(float crownRatio)
        {
            if (crownRatio > 0.11F)
            {
                return 1.0F; // accurate within 0.05%
            }

            // slowdowns typically measured with fifth order polynomial approximation in Douglas-fir benchmark
            // This appears associated with trees falling under the if statement above.
            return 1.0F - MathV.Exp(-(25.0F * 25.0F * crownRatio * crownRatio));
        }

        private float GetDiameterFertilizationAdjustment(FiaCode species, OrganonVariant variant, int simulationStep, float douglasFirSiteIndexFromDbh, OrganonTreatments treatments)
        {
            // fertilization diameter effects currently supported only for non-RAP Douglas-fir
            if ((treatments.HasFertilization == false) || (species != FiaCode.PseudotsugaMenziesii) || (variant.TreeModel != TreeModel.OrganonRap))
            {
                return 1.0F;
            }

            // non-RAP Douglas-fir
            // HANN ET AL.(2003) FRL RESEARCH CONTRIBUTION 40
            float PF1 = 1.368661121F;
            float PF2 = 0.741476964F;
            float PF3 = -0.214741684F;
            float PF4 = -0.851736558F;
            float PF5 = 2.0F;

            float FALDWN = 1.0F;
            float XTIME = Constant.DefaultTimeStepInYears * (float)simulationStep;
            float FERTX1 = 0.0F;
            for (int treatmentIndex = 1; treatmentIndex < 5; ++treatmentIndex)
            {
                FERTX1 += treatments.PoundsOfNitrogenPerAcre[treatmentIndex] / 800.0F * MathV.Exp(PF3 / PF2 * (treatments.FertilizationYears[1] - treatments.FertilizationYears[treatmentIndex]));
            }

            float FERTX2 = MathV.Exp(PF3 * (XTIME - treatments.FertilizationYears[1]) + MathF.Pow(PF4 * (douglasFirSiteIndexFromDbh / 100.0F), PF5));
            float FERTADJ = 1.0F + PF1 * MathV.Pow((treatments.PoundsOfNitrogenPerAcre[1] / 800.0F) + FERTX1, PF2) * FERTX2 * FALDWN;
            Debug.Assert(FERTADJ >= 1.0F);
            return FERTADJ;
        }

        /// <summary>
        /// Find diameter growth multiplier for thinning.
        /// </summary>
        /// <param name="species">FIA species code.</param>
        /// <param name="variant">Organon variant.</param>
        /// <param name="simulationStep">Simulation cycle.</param>
        /// <param name="treatments"></param>
        /// <remarks>
        /// Has special cases for Douglas-fir, western hemlock, and red alder (only for RAP).
        /// </remarks>
        private float GetDiameterThinningAdjustment(FiaCode species, OrganonVariant variant, int simulationStep, OrganonTreatments treatments)
        {
            if (treatments.HasThinning == false)
            {
                return 1.0F;
            }

            // Hann DW, Marshall DD, Hanus ML. 2003. Equations for predicting height-to-crown-base, 5-year diameter-growth-rate, 5-year height-growth rate,
            //   5-year mortality-rate, and maximum size-density trajectory for Douglas-fir and western hemlock in the coasta region of the Pacific
            //   Northwest. FRL Research Contribution 40. https://ir.library.oregonstate.edu/concern/technical_reports/jd472x893
            // table 21
            float a8;
            float PT2;
            float ag;
            if (species == FiaCode.TsugaHeterophylla)
            {
                a8 = 0.723095045F;
                PT2 = 1.0F;
                ag = -0.2644085320F;
            }
            else if (species == FiaCode.PseudotsugaMenziesii)
            {
                a8 = 0.6203827985F;
                PT2 = 1.0F;
                ag = -0.2644085320F;
            }
            else if ((variant.TreeModel == TreeModel.OrganonRap) && (species == FiaCode.AlnusRubra))
            {
                // thinning effects not supported
                return 1.0F;
            }
            else
            {
                a8 = 0.6203827985F;
                PT2 = 1.0F;
                ag = -0.2644085320F;
            }

            float THINX1 = 0.0F;
            for (int treatmentIndex = 1; treatmentIndex < treatments.BasalAreaRemovedByThin.Length; ++treatmentIndex)
            {
                THINX1 += treatments.BasalAreaRemovedByThin[treatmentIndex] * MathV.Exp(ag / PT2 * (treatments.ThinningYears[0] - treatments.ThinningYears[treatmentIndex]));
            }
            float THINX2 = THINX1 + treatments.BasalAreaRemovedByThin[0];
            float THINX3 = THINX1 + treatments.BasalAreaBeforeThin;

            float PREM;
            if (THINX3 <= 0.0F)
            {
                PREM = 0.0F;
            }
            else
            {
                PREM = THINX2 / THINX3;
            }
            if (PREM > 0.75F)
            {
                PREM = 0.75F;
            }

            float XTIME = Constant.DefaultTimeStepInYears * (float)simulationStep;
            float THINADJ = 1.0F + a8 * MathF.Pow(PREM, PT2) * MathV.Exp(ag * (XTIME - treatments.FertilizationYears[1]));
            Debug.Assert(THINADJ >= 1.0F);
            return THINADJ;
        }

        private float GetHeightFertilizationAdjustment(int simulationStep, OrganonVariant variant, FiaCode species, float siteIndexFromBreastHeight, OrganonTreatments treatments)
        {
            // fertilization height effects currently supported only for non-RAP Douglas-fir
            if ((treatments.HasFertilization == false) || (species != FiaCode.PseudotsugaMenziesii) || (variant.TreeModel != TreeModel.OrganonRap))
            {
                return 1.0F;
            }

            // non-RAP Douglas-fir
            float PF1 = 1.0F;
            float PF2 = 0.333333333F;
            float PF3 = -1.107409443F;
            float PF4 = -2.133334346F;
            float PF5 = 1.5F;

            float FALDWN = 1.0F;
            float XTIME = Constant.DefaultTimeStepInYears * (float)simulationStep;
            float FERTX1 = 0.0F;
            for (int index = 1; index < 5; ++index)
            {
                FERTX1 += treatments.PoundsOfNitrogenPerAcre[index] / 800.0F * MathV.Exp((PF3 / PF2) * (treatments.FertilizationYears[0] - treatments.FertilizationYears[index]));
            }
            float FERTX2 = MathV.Exp(PF3 * (XTIME - treatments.FertilizationYears[0]) + PF4 * MathV.Pow(siteIndexFromBreastHeight / 100.0F, PF5));
            float FERTADJ = 1.0F + PF1 * MathV.Pow(treatments.PoundsOfNitrogenPerAcre[0] / 800.0F + FERTX1, PF2) * FERTX2 * FALDWN;
            Debug.Assert(FERTADJ >= 1.0F);
            return FERTADJ;
        }

        private float GetHeightThinningAdjustment(int simulationStep, OrganonVariant variant, FiaCode species, OrganonTreatments treatments)
        {
            if (treatments.HasThinning == false)
            {
                return 1.0F;
            }

            float PT1;
            float PT2;
            float PT3;
            if (variant.TreeModel != TreeModel.OrganonRap)
            {
                if (species == FiaCode.PseudotsugaMenziesii)
                {
                    PT1 = -0.3197415492F;
                    PT2 = 0.7528887377F;
                    PT3 = -0.2268800162F;
                }
                else
                {
                    // thinning effects not supported: avoid subsequent calculation costs
                    return 1.0F;
                }
            }
            else
            {
                if (species == FiaCode.AlnusRubra)
                {
                    PT1 = -0.613313694F;
                    PT2 = 1.0F;
                    PT3 = -0.443824038F;
                }
                else if (species == FiaCode.PseudotsugaMenziesii)
                {
                    PT1 = -0.3197415492F;
                    PT2 = 0.7528887377F;
                    PT3 = -0.2268800162F;
                }
                else
                {
                    // thinning effects not supported
                    return 1.0F;
                }
            }

            float XTIME = 5.0F * (float)simulationStep;
            float THINX1 = 0.0F;
            for (int treatmentIndex = 1; treatmentIndex < 5; ++treatmentIndex)
            {
                THINX1 += treatments.BasalAreaRemovedByThin[treatmentIndex] * MathV.Exp((PT3 / PT2) * (treatments.ThinningYears[0] - treatments.ThinningYears[0]));
            }
            float THINX2 = THINX1 + treatments.BasalAreaRemovedByThin[0];
            float THINX3 = THINX1 + treatments.BasalAreaBeforeThin;
            float PREM;
            if (THINX3 <= 0.0F)
            {
                PREM = 0.0F;
            }
            else
            {
                PREM = THINX2 / THINX3;
            }
            if (PREM > 0.75F)
            {
                PREM = 0.75F;
            }
            float THINADJ = 1.0F + PT1 * MathF.Pow(PREM, PT2) * MathV.Exp(PT3 * (XTIME - treatments.ThinningYears[0]));
            Debug.Assert(THINADJ >= 0.0F);
            return THINADJ;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="simulationStep"></param>
        /// <param name="configuration">Organon growth simulation options and site settings.</param>
        /// <param name="stand"></param>
        /// <param name="ACALIB">Array of calibration coefficients. Values must be between 0.5 and 2.0.</param>
        /// <param name="treatments"></param>
        public static void Grow(int simulationStep, OrganonConfiguration configuration, OrganonStand stand, Dictionary<FiaCode, float[]> ACALIB, OrganonTreatments treatments)
        {
            // BUGBUG: simulationStep largely duplicates stand age
            ValidateArguments(simulationStep, configuration, stand, ACALIB, treatments, out int BIG6, out int BNXT);

            // BUGBUG: 5 * simulationStep is incorrect for RAP
            int simulationYear = Constant.DefaultTimeStepInYears * simulationStep;
            if (configuration.Fertilizer && (treatments.FertilizationYears[0] == (float)simulationYear))
            {
                treatments.FertilizationCycle = 1;
            }

            Dictionary<FiaCode, float[]> CALIB = new Dictionary<FiaCode, float[]>(ACALIB.Count);
            foreach (KeyValuePair<FiaCode, float[]> species in ACALIB)
            {
                float[] speciesCalibration = new float[6];
                CALIB.Add(species.Key, speciesCalibration);

                if (configuration.CalibrateHeight)
                {
                    speciesCalibration[0] = (1.0F + ACALIB[species.Key][0]) / 2.0F + MathV.Pow(0.5F, 0.5F * simulationStep) * ((ACALIB[species.Key][0] - 1.0F) / 2.0F);
                }
                else
                {
                    speciesCalibration[0] = 1.0F;
                }
                if (configuration.CalibrateCrownRatio)
                {
                    speciesCalibration[1] = (1.0F + ACALIB[species.Key][1]) / 2.0F + MathV.Pow(0.5F, 0.5F * simulationStep) * ((ACALIB[species.Key][1] - 1.0F) / 2.0F);
                }
                else
                {
                    speciesCalibration[1] = 1.0F;
                }
                if (configuration.CalibrateDiameter)
                {
                    speciesCalibration[2] = (1.0F + ACALIB[species.Key][2]) / 2.0F + MathV.Pow(0.5F, 0.5F * simulationStep) * ((ACALIB[species.Key][2] - 1.0F) / 2.0F);
                }
                else
                {
                    speciesCalibration[2] = 1.0F;
                }
            }


            // density at start of growth
            stand.SetRedAlderSiteIndexAndGrowthEffectiveAge();
            OrganonStandDensity densityBeforeGrowth = new OrganonStandDensity(stand, configuration.Variant);

            // CCH and crown closure at start of growth
            float[] CCH = OrganonStandDensity.GetCrownCompetitionByHeight(configuration.Variant, stand);
            OrganonGrowth treeGrowth = new OrganonGrowth();
            treeGrowth.Grow(ref simulationStep, configuration, stand, densityBeforeGrowth, CALIB, treatments, ref CCH, out OrganonStandDensity _, out int oldTreeRecordCount);

            if (configuration.IsEvenAge == false)
            {
                stand.AgeInYears = 0;
                stand.BreastHeightAgeInYears = 0;
            }
            float oldTreePercentage = 100.0F * (float)oldTreeRecordCount / (float)(BIG6 - BNXT);
            if (oldTreePercentage > 50.0F)
            {
                stand.Warnings.TreesOld = true;
            }
            if (configuration.Variant.TreeModel == TreeModel.OrganonSwo)
            {
                if (configuration.IsEvenAge && (stand.BreastHeightAgeInYears > 500.0F))
                {
                    stand.Warnings.TreesOld = true;
                }
            }
            else if ((configuration.Variant.TreeModel == TreeModel.OrganonNwo) || (configuration.Variant.TreeModel == TreeModel.OrganonSmc))
            {
                if (configuration.IsEvenAge && (stand.BreastHeightAgeInYears > 120.0F))
                {
                    stand.Warnings.TreesOld = true;
                }
            }
            else
            {
                if (configuration.IsEvenAge && (stand.AgeInYears > 30.0F))
                {
                    stand.Warnings.TreesOld = true;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="simulationStep"></param>
        /// <param name="configuration">Organon configuration settings.</param>
        /// <param name="stand">Stand data.</param>
        /// <param name="densityBeforeGrowth"></param>
        /// <param name="CALIB"></param>
        /// <param name="treatments"></param>
        /// <param name="crownCompetitionByHeight"></param>
        /// <param name="RAAGE"></param>
        /// <param name="densityAfterGrowth"></param>
        /// <param name="oldTreeRecordCount"></param>
        public void Grow(ref int simulationStep, OrganonConfiguration configuration, OrganonStand stand, OrganonStandDensity densityBeforeGrowth,
                         Dictionary<FiaCode, float[]> CALIB, OrganonTreatments treatments, ref float[] crownCompetitionByHeight, 
                         out OrganonStandDensity densityAfterGrowth, out int oldTreeRecordCount)
        {
            oldTreeRecordCount = 0;

            float DGMODgenetic = 1.0F;
            float HGMODgenetic = 1.0F;
            float DGMODSwissNeedleCast = 1.0F;
            float HGMODSwissNeedleCast = 1.0F;
            if ((stand.AgeInYears > 0) && configuration.Genetics)
            {
                OrganonGrowthModifiers.GG_MODS((float)stand.AgeInYears, configuration.GWDG, configuration.GWHG, out DGMODgenetic, out HGMODgenetic);
            }
            if (configuration.SwissNeedleCast && (configuration.Variant.TreeModel == TreeModel.OrganonNwo || configuration.Variant.TreeModel == TreeModel.OrganonSmc))
            {
                OrganonGrowthModifiers.SNC_MODS(configuration.FR, out DGMODSwissNeedleCast, out HGMODSwissNeedleCast);
            }

            // diameter growth
            int treeRecordsWithExpansionFactorZero = 0;
            int bigSixRecordsWithExpansionFactorZero = 0;
            int otherSpeciesRecordsWithExpansionFactorZero = 0;
            foreach (Trees treesOfSpecies in stand.TreesBySpecies.Values)
            {
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    if (treesOfSpecies.LiveExpansionFactor[treeIndex] <= 0.0F)
                    {
                        ++treeRecordsWithExpansionFactorZero;
                        if (configuration.Variant.IsBigSixSpecies(treesOfSpecies.Species))
                        {
                            ++bigSixRecordsWithExpansionFactorZero;
                        }
                        else
                        {
                            ++otherSpeciesRecordsWithExpansionFactorZero;
                        }
                    }
                }
            }

            // BUGBUG no check that SITE_1 and SITE_2 indices are greater than 4.5 feet
            foreach (Trees treesOfSpecies in stand.TreesBySpecies.Values)
            {
                float geneticDiseaseAndCalibrationMultiplier = CALIB[treesOfSpecies.Species][2];
                if (treesOfSpecies.Species == FiaCode.PseudotsugaMenziesii)
                {
                    geneticDiseaseAndCalibrationMultiplier *= DGMODgenetic * DGMODSwissNeedleCast;
                }
                this.GrowDiameter(configuration.Variant, simulationStep, stand, treesOfSpecies, densityBeforeGrowth, geneticDiseaseAndCalibrationMultiplier, treatments);
            }

            // height growth for big six species
            foreach (Trees treesOfSpecies in stand.TreesBySpecies.Values)
            {
                FiaCode species = treesOfSpecies.Species;
                if (configuration.Variant.IsBigSixSpecies(species) == false)
                {
                    continue;
                }

                float geneticAndDiseaseMultiplier = 1.0F;
                if (treesOfSpecies.Species == FiaCode.PseudotsugaMenziesii)
                {
                    geneticAndDiseaseMultiplier = HGMODgenetic * HGMODSwissNeedleCast;
                }
                this.GrowHeightBigSixSpecies(configuration, simulationStep, stand, treesOfSpecies, geneticAndDiseaseMultiplier, crownCompetitionByHeight, treatments, out int oldTreeRecordCountForSpecies);
                oldTreeRecordCount += oldTreeRecordCountForSpecies;
            }

            // determine mortality
            // Sets configuration.NO.
            OrganonMortality.ReduceExpansionFactors(configuration, simulationStep, stand, densityBeforeGrowth, treatments);

            // grow tree diameters
            foreach (Trees treesOfSpecies in stand.TreesBySpecies.Values)
            {
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    treesOfSpecies.Dbh[treeIndex] += treesOfSpecies.DbhGrowth[treeIndex];
                }
            }

            // CALC EOG SBA, CCF/TREE, CCF IN LARGER TREES AND STAND CCF
            densityAfterGrowth = new OrganonStandDensity(stand, configuration.Variant);

            // height growth for non-big six species
            foreach (Trees treesOfSpecies in stand.TreesBySpecies.Values)
            {
                FiaCode species = treesOfSpecies.Species;
                if (configuration.Variant.IsBigSixSpecies(species))
                {
                    continue;
                }

                this.GrowHeightMinorSpecies(configuration, stand, treesOfSpecies, CALIB[species][0]);
            }

            // grow crowns
            crownCompetitionByHeight = this.GrowCrown(configuration.Variant, stand, densityAfterGrowth, CALIB);

            // update stand variables
            if (configuration.Variant.TreeModel != TreeModel.OrganonRap)
            {
                stand.AgeInYears += 5;
                stand.BreastHeightAgeInYears += 5;
            }
            else
            {
                ++stand.AgeInYears;
                ++stand.BreastHeightAgeInYears;
            }
            ++simulationStep;
            if (treatments.FertilizationCycle > 2)
            {
                treatments.FertilizationCycle = 0;
            }
            else if (treatments.FertilizationCycle > 0)
            {
                ++treatments.FertilizationCycle;
            }
            if (treatments.ThinningCycle > 0)
            {
                ++treatments.ThinningCycle;
            }

            // reduce calibration ratios
            foreach (float[] speciesCalibration in CALIB.Values)
            {
                for (int index = 0; index < 3; ++index)
                {
                    if (speciesCalibration[index] != 1.0F)
                    {
                        float MCALIB = (1.0F + speciesCalibration[index + 2]) / 2.0F;
                        speciesCalibration[index] = MCALIB + 0.7071067812F * (speciesCalibration[index] - MCALIB); // 0.7071067812 = MathF.Sqrt(0.5F)
                    }
                }
            }
        }

        public float[] GrowCrown(OrganonVariant variant, OrganonStand stand, OrganonStandDensity densityAfterGrowth, Dictionary<FiaCode, float[]> CALIB)
        {
            float oldGrowthIndicator = OrganonMortality.GetOldGrowthIndicator(variant, stand);
            foreach (Trees treesOfSpecies in stand.TreesBySpecies.Values)
            {
                variant.GrowCrown(stand, treesOfSpecies, densityAfterGrowth, oldGrowthIndicator, CALIB[treesOfSpecies.Species][1]);
            }

            return OrganonStandDensity.GetCrownCompetitionByHeight(variant, stand);
        }

        public void GrowDiameter(OrganonVariant variant, int simulationStep, OrganonStand stand, Trees trees, OrganonStandDensity densityBeforeGrowth, float geneticDiseaseAndCalibrationMultiplier, OrganonTreatments treatments)
        {
            FiaCode species = trees.Species;
            float siteIndex = stand.SiteIndex;
            if (species == FiaCode.TsugaHeterophylla)
            {
                siteIndex = stand.HemlockSiteIndex;
            }
            // questionable descisions retained from Fortran code due to calibration fragility:
            // - ponderosa index isn't used for SWO
            // - red alder index isn't used for red alder

            float fertilizationAdjustment = this.GetDiameterFertilizationAdjustment(species, variant, simulationStep, stand.SiteIndex - 4.5F, treatments);
            float thinningAdjustment = this.GetDiameterThinningAdjustment(species, variant, simulationStep, treatments);
            float combinedAdjustment = geneticDiseaseAndCalibrationMultiplier * fertilizationAdjustment * thinningAdjustment;
            variant.GrowDiameter(trees, combinedAdjustment, siteIndex, densityBeforeGrowth);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="configuration">Organon configuration.</param>
        /// <param name="simulationStep">Simulation cycle.</param>
        /// <param name="stand">Stand data.</param>
        /// <param name="trees">Trees to calculate height growth of.</param>
        /// <param name="geneticAndDiseaseMultiplier">Direct multiplier for genetic and disease height growth effects.</param>
        /// <param name="crownCompetitionByHeight"></param>
        /// <param name="treatments"></param>
        /// <param name="oldTreeRecordCount"></param>
        public void GrowHeightBigSixSpecies(OrganonConfiguration configuration, int simulationStep, OrganonStand stand, Trees trees, 
                                            float geneticAndDiseaseMultiplier, float[] crownCompetitionByHeight, OrganonTreatments treatments, out int oldTreeRecordCount)
        {
            FiaCode species = trees.Species;
            Debug.Assert(configuration.Variant.IsBigSixSpecies(species));
            oldTreeRecordCount = configuration.Variant.GrowHeightBigSix(configuration, stand, trees, crownCompetitionByHeight);

            float fertilizationAdjustment = this.GetHeightFertilizationAdjustment(simulationStep, configuration.Variant, species, stand.SiteIndex, treatments);
            float thinningAdjustement = this.GetHeightThinningAdjustment(simulationStep, configuration.Variant, species, treatments);
            float combinedAdjustment = geneticAndDiseaseMultiplier * thinningAdjustement * fertilizationAdjustment;
            this.LimitAndApplyHeightGrowth(configuration.Variant, trees, combinedAdjustment);
        }

        public void GrowHeightMinorSpecies(OrganonConfiguration configuration, OrganonStand stand, Trees trees, float calibrationMultiplier)
        {
            FiaCode species = trees.Species;
            Debug.Assert(configuration.Variant.IsBigSixSpecies(species) == false);

            // special case for non-RAP red alders
            if ((species == FiaCode.AlnusRubra) && (configuration.Variant.TreeModel != TreeModel.OrganonRap))
            {
                for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                {
                    if (trees.LiveExpansionFactor[treeIndex] <= 0.0F)
                    {
                        trees.HeightGrowth[treeIndex] = 0.0F;
                        continue;
                    }

                    float growthEffectiveAge = RedAlder.GetGrowthEffectiveAge(trees.Height[treeIndex], stand.RedAlderSiteIndex);
                    if (growthEffectiveAge <= 0.0F)
                    {
                        trees.HeightGrowth[treeIndex] = 0.0F;
                    }
                    else
                    {
                        float RAH1 = RedAlder.GetH50(growthEffectiveAge, stand.RedAlderSiteIndex);
                        float RAH2 = RedAlder.GetH50(growthEffectiveAge + Constant.DefaultTimeStepInYears, stand.RedAlderSiteIndex);
                        trees.HeightGrowth[treeIndex] = RAH2 - RAH1;
                        Debug.Assert(trees.HeightGrowth[treeIndex] >= 0.0F);
                        Debug.Assert(trees.HeightGrowth[treeIndex] < Constant.Maximum.HeightIncrementInFeet);
                        trees.Height[treeIndex] += trees.HeightGrowth[treeIndex];
                    }
                }
                return;
            }

            // mainline case for all other species and Organon variants
            // TODO: could previous predicted height or pre-growth height be used?
            configuration.Variant.GetHeightPredictionCoefficients(species, out float B0, out float B1, out float B2);
            for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
            {
                if (trees.LiveExpansionFactor[treeIndex] <= 0.0F)
                {
                    trees.HeightGrowth[treeIndex] = 0.0F;
                    continue;
                }

                float endDbhInInches = trees.Dbh[treeIndex];
                float endPredictedHeight = 4.5F + MathV.Exp(B0 + B1 * MathV.Pow(endDbhInInches, B2));
                endPredictedHeight = 4.5F + calibrationMultiplier * (endPredictedHeight - 4.5F);

                float startDbhInInches = endDbhInInches - trees.DbhGrowth[treeIndex];
                float startPredictedHeight = 4.5F + MathV.Exp(B0 + B1 * MathV.Pow(startDbhInInches, B2));
                startPredictedHeight = 4.5F + calibrationMultiplier * (startPredictedHeight - 4.5F);

                float predictedHeight = (endPredictedHeight / startPredictedHeight) * trees.Height[treeIndex];
                trees.HeightGrowth[treeIndex] = predictedHeight - trees.Height[treeIndex];

                Debug.Assert(trees.HeightGrowth[treeIndex] >= 0.0F);
                Debug.Assert(trees.HeightGrowth[treeIndex] < Constant.Maximum.HeightIncrementInFeet);
                trees.Height[treeIndex] += trees.HeightGrowth[treeIndex];
            }
        }

        private void LimitAndApplyHeightGrowth(OrganonVariant variant, Trees trees, float combinedAdjustment)
        {
            FiaCode speciesWithSwoTsheOptOut = trees.Species;
            if ((speciesWithSwoTsheOptOut == FiaCode.TsugaHeterophylla) && (variant.TreeModel == TreeModel.OrganonSwo))
            {
                // BUGBUG: not clear why SWO uses default coefficients for hemlock
                speciesWithSwoTsheOptOut = FiaCode.NotholithocarpusDensiflorus;
            }

            float A0;
            float A1;
            float A2 = 1.0F;
            switch (speciesWithSwoTsheOptOut)
            {
                case FiaCode.PseudotsugaMenziesii:
                case FiaCode.TsugaHeterophylla:
                    A0 = 19.04942539F;
                    A1 = -0.04484724F;
                    break;
                case FiaCode.AbiesConcolor:
                case FiaCode.AbiesGrandis:
                    A0 = 16.26279948F;
                    A1 = -0.04484724F;
                    break;
                case FiaCode.PinusPonderosa:
                    A0 = 17.11482201F;
                    A1 = -0.04484724F;
                    break;
                case FiaCode.PinusLambertiana:
                    A0 = 14.29011403F;
                    A1 = -0.04484724F;
                    break;
                case FiaCode.AlnusRubra:
                    A0 = 60.619859F;
                    A1 = -1.59138564F;
                    A2 = 0.496705997F;
                    break;
                default:
                    A0 = 15.80319194F;
                    A1 = -0.04484724F;
                    break;
            }

            for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
            {
                if (trees.LiveExpansionFactor[treeIndex] <= 0.0F)
                {
                    continue;
                }

                float HG = combinedAdjustment * trees.HeightGrowth[treeIndex];
                float HT1 = trees.Height[treeIndex] - 4.5F;
                float HT2 = HT1 + HG;
                float HT3 = HT2 + HG;
                float DG = trees.DbhGrowth[treeIndex];
                float DBH1 = trees.Dbh[treeIndex];
                float DBH2 = DBH1 + DG;
                float DBH3 = DBH2 + DG;
                float PHT1;
                float PHT2;
                float PHT3;
                if (A2 == 1.0F)
                {
                    // most species
                    PHT1 = A0 * DBH1 / (1.0F - A1 * DBH1);
                    PHT2 = A0 * DBH2 / (1.0F - A1 * DBH2);
                    PHT3 = A0 * DBH3 / (1.0F - A1 * DBH3);
                }
                else
                {
                    // red alder
                    PHT1 = A0 * DBH1 / (1.0F - A1 * MathV.Pow(DBH1, A2));
                    PHT2 = A0 * DBH2 / (1.0F - A1 * MathV.Pow(DBH2, A2));
                    PHT3 = A0 * DBH3 / (1.0F - A1 * MathV.Pow(DBH3, A2));
                }

                float PHGR1 = (PHT2 - PHT1 + HG) / 2.0F;
                float PHGR2 = PHT2 - HT1;
                if (HT2 > PHT2)
                {
                    if (PHGR1 < PHGR2)
                    {
                        HG = PHGR1;
                    }
                    else
                    {
                        HG = PHGR2;
                    }
                }
                else
                {
                    if (HT3 > PHT3)
                    {
                        HG = PHGR1;
                    }
                }
                if (HG < 0.0F)
                {
                    HG = 0.0F;
                }

                Debug.Assert(HG >= 0.0F);
                Debug.Assert(HG < Constant.Maximum.HeightIncrementInFeet);
                trees.HeightGrowth[treeIndex] = HG;
                trees.Height[treeIndex] += HG;
            }
        }

        /// <summary>
        /// Sets height and crown ratio of ingrowth appended at the end of a list of trees.
        /// </summary>
        /// <param name="variant">Organon variant.</param>
        /// <param name="stand">Stand data.</param>
        /// <param name="ingrowthCount">Number of new trees.</param>
        /// <param name="ACALIB"></param>
        public static void SetIngrowthHeightAndCrownRatio(OrganonVariant variant, OrganonStand stand, Trees treesOfSpecies, int ingrowthCount, Dictionary<FiaCode, float[]> ACALIB)
        {
            if (ingrowthCount > treesOfSpecies.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(ingrowthCount));
            }

            // ROUTINE TO CALCULATE MISSING CROWN RATIOS
            // BUGBUG: does this duplicate site index code elsewhere?
            // NINGRO = NUMBER OF TREES ADDED
            float SITE_1 = stand.SiteIndex;
            float SITE_2 = stand.HemlockSiteIndex;
            float SI_1;
            if (variant.TreeModel == TreeModel.OrganonSwo)
            {
                if ((SITE_1 < 0.0F) && (SITE_2 > 0.0F))
                {
                    SITE_1 = 1.062934F * SITE_2;
                }
                else if (SITE_2 < 0.0F)
                {
                    SITE_2 = 0.940792F * SITE_1;
                }
            }
            else if ((variant.TreeModel == TreeModel.OrganonNwo) || (variant.TreeModel == TreeModel.OrganonSmc))
            {
                if ((SITE_1 < 0.0F) && (SITE_2 > 0.0F))
                {
                    SITE_1 = 0.480F + (1.110F * SITE_2);
                }
                else if (SITE_2 < 0.0F)
                {
                    SITE_2 = -0.432F + (0.899F * SITE_1);
                }
            }
            else
            {
                if (SITE_2 < 0.0F)
                {
                    // BUGBUG: not initialized in Fortran code; should this be SITE_1?
                    SI_1 = 0.0F;
                    SITE_2 = 4.776377F * MathV.Pow(SI_1, 0.763530587F);
                }
            }

            SI_1 = SITE_1 - 4.5F;
            float SI_2 = SITE_2 - 4.5F;
            variant.GetHeightPredictionCoefficients(treesOfSpecies.Species, out float B0, out float B1, out float B2);
            for (int treeIndex = treesOfSpecies.Count - ingrowthCount; treeIndex < treesOfSpecies.Count; ++treeIndex)
            {
                float heightInFeet = treesOfSpecies.Height[treeIndex];
                if (heightInFeet != 0.0F)
                {
                    continue;
                }

                float dbhInInches = treesOfSpecies.Dbh[treeIndex];
                float predictedHeight = 4.5F + MathV.Exp(B0 + B1 * MathV.Pow(dbhInInches, B2));
                treesOfSpecies.Height[treeIndex] = 4.5F + ACALIB[treesOfSpecies.Species][0] * (predictedHeight - 4.5F);
            }

            float OG = OrganonMortality.GetOldGrowthIndicator(variant, stand);
            OrganonStandDensity standDensity = new OrganonStandDensity(stand, variant);
            FiaCode species = treesOfSpecies.Species;
            for (int treeIndex = treesOfSpecies.Count - ingrowthCount; treeIndex < treesOfSpecies.Count; ++treeIndex)
            {
                float crownRatio = treesOfSpecies.CrownRatio[treeIndex];
                if (crownRatio != 0.0F)
                {
                    continue;
                }

                float dbhInInches = treesOfSpecies.Dbh[treeIndex];
                float heightInFeet = treesOfSpecies.Height[treeIndex];
                float crownCompetitionFactorLarger = standDensity.GetCrownCompetitionFactorLarger(dbhInInches);
                float heightToCrownBase = variant.GetHeightToCrownBase(species, heightInFeet, dbhInInches, crownCompetitionFactorLarger, standDensity.BasalAreaPerAcre, SI_1, SI_2, OG);
                if (heightToCrownBase < 0.0F)
                {
                    heightToCrownBase = 0.0F;
                }
                if (heightToCrownBase > 0.95F * heightInFeet)
                {
                    heightToCrownBase = 0.95F * heightInFeet;
                }
                treesOfSpecies.CrownRatio[treeIndex] = (1.0F - (heightToCrownBase / heightInFeet)) * ACALIB[species][1];
            }
        }

        /// <summary>
        /// Does argument checking and raises error flags if problems are found.
        /// </summary>
        /// <param name="simulationStep"></param>
        /// <param name="configuration">Organon configuration settings.</param>
        /// <param name="stand"></param>
        /// <param name="ACALIB"></param>
        /// <param name="treatments"></param>
        /// <param name="BIG6"></param>
        /// <param name="BNXT"></param>
        private static void ValidateArguments(int simulationStep, OrganonConfiguration configuration, OrganonStand stand, Dictionary<FiaCode, float[]> ACALIB, OrganonTreatments treatments,
                                              out int BIG6, out int BNXT)
        {
            if (stand.GetTreeRecordCount() < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(stand.TreesBySpecies));
            }
            if (Enum.IsDefined(typeof(TreeModel), configuration.Variant.TreeModel) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(configuration.Variant));
            }
            if (stand.NumberOfPlots < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stand.NumberOfPlots));
            }
            if ((stand.SiteIndex <= 0.0F) || (stand.SiteIndex > Constant.Maximum.SiteIndexInFeet))
            {
                throw new ArgumentOutOfRangeException(nameof(stand.SiteIndex));
            }
            if ((stand.HemlockSiteIndex <= 0.0F) || (stand.HemlockSiteIndex > Constant.Maximum.SiteIndexInFeet))
            {
                throw new ArgumentOutOfRangeException(nameof(stand.HemlockSiteIndex));
            }

            if (configuration.IsEvenAge)
            {
                if (stand.BreastHeightAgeInYears < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(stand.BreastHeightAgeInYears), nameof(stand.BreastHeightAgeInYears) + " must be zero or greater when " + nameof(configuration.IsEvenAge) + " is set.");
                }
                if ((stand.AgeInYears - stand.BreastHeightAgeInYears) < 1)
                {
                    // (DOUG? can stand.AgeInYears ever be less than stand.BreastHeightAgeInYears?)
                    throw new ArgumentException(nameof(stand.AgeInYears) + " must be greater than " + nameof(stand.BreastHeightAgeInYears) + " when " + nameof(configuration.IsEvenAge) + " is set.");
                }
            }
            else
            {
                if (stand.BreastHeightAgeInYears != 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(stand.BreastHeightAgeInYears), nameof(stand.BreastHeightAgeInYears) + " must be zero or less when " + nameof(configuration.IsEvenAge) + " is not set.");
                }
                if (configuration.Fertilizer)
                {
                    throw new ArgumentException("If " + nameof(configuration.Fertilizer) + " is set " + nameof(configuration.IsEvenAge) + "must also be set.");
                }
            }
            for (int treatmentIndex = 0; treatmentIndex < 5; ++treatmentIndex)
            {
                if (!configuration.Fertilizer && (treatments.FertilizationYears[treatmentIndex] != 0 || treatments.PoundsOfNitrogenPerAcre[treatmentIndex] != 0))
                {
                    throw new ArgumentException();
                }
                if (configuration.Fertilizer)
                {
                    if ((treatments.FertilizationYears[treatmentIndex] > stand.AgeInYears) || (treatments.FertilizationYears[treatmentIndex] > 70.0F))
                    {
                        throw new ArgumentException();
                    }
                    if (treatmentIndex == 0)
                    {
                        if ((treatments.PoundsOfNitrogenPerAcre[treatmentIndex] < 0.0) || (treatments.PoundsOfNitrogenPerAcre[treatmentIndex] > 400.0F))
                        {
                            throw new ArgumentException();
                        }
                        else
                        {
                            if (treatments.PoundsOfNitrogenPerAcre[treatmentIndex] > 400.0F)
                            {
                                throw new ArgumentException();
                            }
                        }
                    }
                }
            }

            if (configuration.Thin && (treatments.BasalAreaRemovedByThin[0] >= treatments.BasalAreaBeforeThin))
            {
                throw new ArgumentException("The first element of " + nameof(treatments.BasalAreaRemovedByThin) + " must be less than " + nameof(treatments.BasalAreaBeforeThin) + " when thinning response is enabled.");
            }
            for (int treatmentIndex = 0; treatmentIndex < 5; ++treatmentIndex)
            {
                if (!configuration.Thin && (treatments.ThinningYears[treatmentIndex] != 0 || treatments.BasalAreaRemovedByThin[treatmentIndex] != 0))
                {
                    throw new ArgumentException();
                }
                if (configuration.Thin)
                {
                    if (configuration.IsEvenAge && treatments.ThinningYears[treatmentIndex] > stand.AgeInYears)
                    {
                        throw new ArgumentException();
                    }
                    if (treatmentIndex > 1)
                    {
                        if (treatments.ThinningYears[treatmentIndex] != 0.0F && treatments.BasalAreaRemovedByThin[treatmentIndex] < 0.0F)
                        {
                            throw new ArgumentException();
                        }
                    }
                    if (treatments.BasalAreaBeforeThin < 0.0F)
                    {
                        throw new ArgumentException();
                    }
                }
            }

            if (simulationStep < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationStep));
            }

            if (configuration.DefaultMaximumSdi > Constant.Maximum.Sdi)
            {
                throw new ArgumentOutOfRangeException(nameof(configuration.DefaultMaximumSdi));
            }
            if (configuration.TrueFirMaximumSdi > Constant.Maximum.Sdi)
            {
                throw new ArgumentOutOfRangeException(nameof(configuration.TrueFirMaximumSdi));
            }
            if (configuration.HemlockMaximumSdi > Constant.Maximum.Sdi)
            {
                throw new ArgumentOutOfRangeException(nameof(configuration.HemlockMaximumSdi));
            }

            if (configuration.Genetics)
            {
                if (!configuration.IsEvenAge)
                {
                    throw new ArgumentOutOfRangeException(nameof(configuration.Genetics), nameof(configuration.Genetics) + " is supported only when " + nameof(configuration.IsEvenAge) + " is set.");
                }
                if ((configuration.GWDG < 0.0F) || (configuration.GWDG > 20.0F))
                {
                    throw new ArgumentOutOfRangeException(nameof(configuration.GWDG));
                }
                if ((configuration.GWHG < 0.0F) || (configuration.GWHG > 20.0F))
                {
                    throw new ArgumentOutOfRangeException(nameof(configuration.GWHG));
                }
            }
            else
            {
                if (configuration.GWDG != 0.0F)
                {
                    throw new ArgumentOutOfRangeException(nameof(configuration.GWDG));
                }
                if (configuration.GWHG != 0.0F)
                {
                    throw new ArgumentOutOfRangeException(nameof(configuration.GWHG));
                }
            }

            if (configuration.SwissNeedleCast)
            {
                if ((configuration.Variant.TreeModel == TreeModel.OrganonSwo) || (configuration.Variant.TreeModel == TreeModel.OrganonRap))
                {
                    throw new ArgumentOutOfRangeException(nameof(configuration.Variant), "Swiss needle cast is not supported by the SWO and RAP variants.");
                }
                if (!configuration.IsEvenAge)
                {
                    throw new ArgumentOutOfRangeException(nameof(configuration.IsEvenAge), "Swiss needle cast is not supported for uneven age stands.");
                }
                if ((configuration.FR < 0.85F) || (configuration.FR > 7.0F))
                {
                    throw new ArgumentOutOfRangeException(nameof(configuration.FR));
                }
                if (configuration.Fertilizer && (configuration.FR < 3.0))
                {
                    throw new ArgumentOutOfRangeException(nameof(configuration.FR), nameof(configuration.FR) + " must be 3.0 or greater when " + nameof(configuration.SwissNeedleCast) + " and " + nameof(configuration.Fertilizer) + "are set.");
                }
            }
            else
            {
                if (configuration.FR > 0.0F)
                {
                    throw new ArgumentOutOfRangeException(nameof(configuration.FR));
                }
            }

            if ((configuration.Variant.TreeModel >= TreeModel.OrganonRap) && (stand.SiteIndex < 0.0F))
            {
                throw new ArgumentOutOfRangeException(nameof(stand.SiteIndex));
            }
            if ((configuration.Variant.TreeModel >= TreeModel.OrganonRap) && (configuration.PDEN < 0.0F))
            {
                throw new ArgumentOutOfRangeException(nameof(configuration.PDEN));
            }
            if (!configuration.IsEvenAge && (configuration.Variant.TreeModel >= TreeModel.OrganonRap))
            {
                throw new ArgumentOutOfRangeException(nameof(configuration.IsEvenAge));
            }

            // TODO: is it desirable to clear existing stand warnings?
            stand.Warnings.BigSixHeightAbovePotential = false;
            stand.Warnings.LessThan50TreeRecords = false;
            stand.Warnings.HemlockSiteIndexOutOfRange = false;
            stand.Warnings.OtherSpeciesBasalAreaTooHigh = false;
            stand.Warnings.SiteIndexOutOfRange = false;
            stand.Warnings.TreesOld = false;
            stand.Warnings.TreesYoung = false;

            foreach (float[] speciesCalibration in ACALIB.Values)
            {
                if ((speciesCalibration.Length != 6) ||
                    (speciesCalibration[0] < 0.5F) || (speciesCalibration[0] > 2.0F) ||
                    (speciesCalibration[1] < 0.5F) || (speciesCalibration[1] > 2.0F) ||
                    (speciesCalibration[2] < 0.5F) || (speciesCalibration[2] > 2.0F))
                {
                    throw new ArgumentOutOfRangeException(nameof(ACALIB));
                }
            }

            // check tree records for errors
            foreach (Trees treesOfSpecies in stand.TreesBySpecies.Values)
            {
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    if (configuration.Variant.IsSpeciesSupported(treesOfSpecies.Species) == false)
                    {
                        throw new NotSupportedException(String.Format("{0} does not support {1} (tree {2}).", configuration.Variant.TreeModel, treesOfSpecies.Species, treeIndex));
                    }
                    float dbhInInches = treesOfSpecies.Dbh[treeIndex];
                    if (dbhInInches < 0.09F)
                    {
                        throw new NotSupportedException(String.Format("Diameter of tree {0} is less than 0.1 inches.", treeIndex));
                    }
                    float heightInFeet = treesOfSpecies.Height[treeIndex];
                    if (heightInFeet < 4.5F)
                    {
                        throw new NotSupportedException(String.Format("Height of tree {0} is less than 4.5 feet.", treeIndex));
                    }
                    float crownRatio = treesOfSpecies.CrownRatio[treeIndex];
                    if ((crownRatio < 0.0F) || (crownRatio > 1.0F))
                    {
                        throw new NotSupportedException(String.Format("Crown ratio of tree {0} is not between 0 and 1.", treeIndex));
                    }
                    float expansionFactor = treesOfSpecies.LiveExpansionFactor[treeIndex];
                    if (expansionFactor < 0.0F)
                    {
                        throw new NotSupportedException(String.Format("Expansion factor of tree {0} is negative.", treeIndex));
                    }
                }
            }

            BIG6 = 0;
            BNXT = 0;
            float maxGrandFirHeight = 0.0F;
            float maxDouglasFirHeight = 0.0F;
            float maxWesternHemlockHeight = 0.0F;
            float maxPonderosaHeight = 0.0F;
            float maxIncenseCedarHeight = 0.0F;
            float maxRedAlderHeight = 0.0F;
            foreach (Trees treesOfSpecies in stand.TreesBySpecies.Values)
            {
                FiaCode species = treesOfSpecies.Species;
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    float heightInFeet = treesOfSpecies.Height[treeIndex];
                    switch (configuration.Variant.TreeModel)
                    {
                        // SWO BIG SIX
                        case TreeModel.OrganonSwo:
                            if ((species == FiaCode.PinusPonderosa) && (heightInFeet > maxPonderosaHeight))
                            {
                                maxPonderosaHeight = heightInFeet;
                            }
                            else if ((species == FiaCode.CalocedrusDecurrens) && (heightInFeet > maxIncenseCedarHeight))
                            {
                                maxIncenseCedarHeight = heightInFeet;
                            }
                            else if ((species == FiaCode.PseudotsugaMenziesii) && (heightInFeet > maxDouglasFirHeight))
                            {
                                maxDouglasFirHeight = heightInFeet;
                            }
                            // BUGBUG: why are true firs and sugar pine being assigned to Douglas-fir max height?
                            else if ((species == FiaCode.AbiesConcolor) && (heightInFeet > maxDouglasFirHeight))
                            {
                                maxDouglasFirHeight = heightInFeet;
                            }
                            else if ((species == FiaCode.AbiesGrandis) && (heightInFeet > maxDouglasFirHeight))
                            {
                                maxDouglasFirHeight = heightInFeet;
                            }
                            else if ((species == FiaCode.PinusLambertiana) && (heightInFeet > maxDouglasFirHeight))
                            {
                                maxDouglasFirHeight = heightInFeet;
                            }
                            break;
                        case TreeModel.OrganonNwo:
                        case TreeModel.OrganonSmc:
                            if ((species == FiaCode.AbiesGrandis) && (heightInFeet > maxGrandFirHeight))
                            {
                                maxGrandFirHeight = heightInFeet;
                            }
                            else if ((species == FiaCode.PseudotsugaMenziesii) && (heightInFeet > maxDouglasFirHeight))
                            {
                                maxDouglasFirHeight = heightInFeet;
                            }
                            else if ((species == FiaCode.TsugaHeterophylla) && (heightInFeet > maxWesternHemlockHeight))
                            {
                                maxWesternHemlockHeight = heightInFeet;
                            }
                            break;
                        case TreeModel.OrganonRap:
                            if ((species == FiaCode.AlnusRubra) && (heightInFeet > maxRedAlderHeight))
                            {
                                maxRedAlderHeight = heightInFeet;
                            }
                            break;
                    }

                    if (configuration.Variant.IsBigSixSpecies(species))
                    {
                        ++BIG6;
                        if (treesOfSpecies.LiveExpansionFactor[treeIndex] < 0.0F)
                        {
                            ++BNXT;
                        }
                    }
                }
            }

            // DETERMINE IF SPECIES MIX CORRECT FOR STAND AGE
            float standBasalArea = 0.0F;
            float standBigSixBasalArea = 0.0F;
            float standHardwoodBasalArea = 0.0F;
            foreach (Trees treesOfSpecies in stand.TreesBySpecies.Values)
            {
                FiaCode species = treesOfSpecies.Species;
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    float expansionFactor = treesOfSpecies.LiveExpansionFactor[treeIndex];
                    if (expansionFactor <= 0.0F)
                    {
                        continue;
                    }

                    float dbhInInches = treesOfSpecies.Dbh[treeIndex];
                    float basalArea = expansionFactor * dbhInInches * dbhInInches;
                    standBasalArea += basalArea;

                    if (configuration.Variant.IsBigSixSpecies(species))
                    {
                        standBigSixBasalArea += basalArea;
                    }
                    if (configuration.Variant.TreeModel == TreeModel.OrganonSwo)
                    {
                        if ((species == FiaCode.ArbutusMenziesii) || (species == FiaCode.ChrysolepisChrysophyllaVarChrysophylla) || (species == FiaCode.QuercusKelloggii))
                        {
                            standHardwoodBasalArea += basalArea;
                        }
                    }
                }
            }

            standBasalArea *= Constant.ForestersEnglish / stand.NumberOfPlots;
            standBigSixBasalArea *= Constant.ForestersEnglish / stand.NumberOfPlots;
            if (standBigSixBasalArea < 0.0F)
            {
                throw new NotSupportedException("Total basal area big six species is negative.");
            }

            if (configuration.Variant.TreeModel >= TreeModel.OrganonRap)
            {
                float PRA;
                if (standBasalArea > 0.0F)
                {
                    PRA = standBigSixBasalArea / standBasalArea;
                }
                else
                {
                    PRA = 0.0F;
                }

                if (PRA < 0.9F)
                {
                    // if needed, make this a warning rather than an error
                    throw new NotSupportedException("Red alder plantation stand is less than 90% by basal area.");
                }
            }

            // DETERMINE WARNINGS (IF ANY)
            // BUGBUG move maximum site indices to variant capabilities
            switch (configuration.Variant.TreeModel)
            {
                case TreeModel.OrganonSwo:
                    if ((stand.SiteIndex > 0.0F) && ((stand.SiteIndex < 40.0F) || (stand.SiteIndex > 150.0F)))
                    {
                        stand.Warnings.SiteIndexOutOfRange = true;
                    }
                    if ((stand.HemlockSiteIndex > 0.0F) && ((stand.HemlockSiteIndex < 50.0F) || (stand.HemlockSiteIndex > 140.0F)))
                    {
                        stand.Warnings.HemlockSiteIndexOutOfRange = true;
                    }
                    break;
                case TreeModel.OrganonNwo:
                case TreeModel.OrganonSmc:
                    if ((stand.SiteIndex > 0.0F) && ((stand.SiteIndex < 90.0F) || (stand.SiteIndex > 142.0F)))
                    {
                        stand.Warnings.SiteIndexOutOfRange = true;
                    }
                    if ((stand.HemlockSiteIndex > 0.0F) && ((stand.HemlockSiteIndex < 90.0F) || (stand.HemlockSiteIndex > 142.0F)))
                    {
                        stand.Warnings.HemlockSiteIndexOutOfRange = true;
                    }
                    break;
                case TreeModel.OrganonRap:
                    if ((stand.SiteIndex < 20.0F) || (stand.SiteIndex > 125.0F))
                    {
                        stand.Warnings.SiteIndexOutOfRange = true;
                    }
                    if ((stand.HemlockSiteIndex > 0.0F) && (stand.HemlockSiteIndex < 90.0F || stand.HemlockSiteIndex > 142.0F))
                    {
                        stand.Warnings.HemlockSiteIndexOutOfRange = true;
                    }
                    break;
            }

            // check tallest trees in stand against maximum height for big six species
            // BUGBUG: need an API for maximum heights rather than inline code here
            switch (configuration.Variant.TreeModel)
            {
                case TreeModel.OrganonSwo:
                    if (maxPonderosaHeight > 0.0F)
                    {
                        float MAXHT = (stand.HemlockSiteIndex - 4.5F) * (1.0F / (1.0F - MathV.Exp(MathF.Pow(-0.164985F * (stand.HemlockSiteIndex - 4.5F), 0.288169F)))) + 4.5F;
                        if (maxPonderosaHeight > MAXHT)
                        {
                            stand.Warnings.BigSixHeightAbovePotential = true;
                        }
                    }
                    if (maxIncenseCedarHeight > 0.0F)
                    {
                        float ICSI = (0.66F * stand.SiteIndex) - 4.5F;
                        float MAXHT = ICSI * (1.0F / (1.0F - MathV.Exp(MathF.Pow(-0.174929F * ICSI, 0.281176F)))) + 4.5F;
                        if (maxIncenseCedarHeight > MAXHT)
                        {
                            stand.Warnings.BigSixHeightAbovePotential = true;
                        }
                    }
                    if (maxDouglasFirHeight > 0.0F)
                    {
                        float MAXHT = (stand.SiteIndex - 4.5F) * (1.0F / (1.0F - MathV.Exp(MathF.Pow(-0.174929F * (stand.SiteIndex - 4.5F), 0.281176F)))) + 4.5F;
                        if (maxDouglasFirHeight > MAXHT)
                        {
                            stand.Warnings.BigSixHeightAbovePotential = true;
                        }
                    }
                    break;
                case TreeModel.OrganonNwo:
                case TreeModel.OrganonSmc:
                    if (maxDouglasFirHeight > 0.0F)
                    {
                        float Z50 = 2500.0F / (stand.SiteIndex - 4.5F);
                        float MAXHT = 4.5F + 1.0F / (-0.000733819F + 0.000197693F * Z50);
                        if (maxDouglasFirHeight > MAXHT)
                        {
                            stand.Warnings.BigSixHeightAbovePotential = true;
                        }
                    }
                    if (maxGrandFirHeight > 0.0F)
                    {
                        float Z50 = 2500.0F / (stand.SiteIndex - 4.5F);
                        float MAXHT = 4.5F + 1.0F / (-0.000733819F + 0.000197693F * Z50);
                        if (maxGrandFirHeight > MAXHT)
                        {
                            stand.Warnings.BigSixHeightAbovePotential = true;
                        }
                    }
                    if (maxWesternHemlockHeight > 0.0F)
                    {
                        float Z50 = 2500.0F / (stand.HemlockSiteIndex - 4.5F);
                        float MAXHT = 4.5F + 1.0F / (0.00192F + 0.00007F * Z50);
                        if (maxWesternHemlockHeight > MAXHT)
                        {
                            stand.Warnings.BigSixHeightAbovePotential = true;
                        }
                    }
                    break;
                case TreeModel.OrganonRap:
                    if (maxRedAlderHeight > 0.0F)
                    {
                        RedAlder.WHHLB_H40(stand.SiteIndex, 20.0F, 150.0F, out float MAXHT);
                        if (maxRedAlderHeight > MAXHT)
                        {
                            stand.Warnings.BigSixHeightAbovePotential = true;
                        }
                    }
                    break;
            }

            if (configuration.IsEvenAge && (configuration.Variant.TreeModel != TreeModel.OrganonSmc))
            {
                stand.Warnings.TreesYoung = stand.BreastHeightAgeInYears < 10;
            }

            float requiredWellKnownSpeciesBasalAreaFraction = configuration.Variant.TreeModel switch
            {
                TreeModel.OrganonNwo => 0.5F,
                TreeModel.OrganonRap => 0.8F,
                TreeModel.OrganonSmc => 0.5F,
                TreeModel.OrganonSwo => 0.2F,
                _ => throw OrganonVariant.CreateUnhandledModelException(configuration.Variant.TreeModel),
            };
            if ((standBigSixBasalArea + standHardwoodBasalArea) < (requiredWellKnownSpeciesBasalAreaFraction * standBasalArea))
            {
                stand.Warnings.OtherSpeciesBasalAreaTooHigh = true;
            }
            if (stand.GetTreeRecordCount() < 50)
            {
                stand.Warnings.LessThan50TreeRecords = true;
            }

            // check percentage of trees with high growth effective ages
            int oldTreeRecordCount = 0;
            foreach (Trees treesOfSpecies in stand.TreesBySpecies.Values)
            {
                FiaCode species = treesOfSpecies.Species;
                if (configuration.Variant.IsBigSixSpecies(species) == false)
                {
                    continue;
                }

                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    float height = treesOfSpecies.Height[treeIndex];
                    if (height < 4.5F)
                    {
                        continue;
                    }

                    float growthEffectiveAge = configuration.Variant.GetGrowthEffectiveAge(configuration, stand, treesOfSpecies, treeIndex, out float _);
                    if (growthEffectiveAge > configuration.Variant.OldTreeAgeThreshold)
                    {
                        ++oldTreeRecordCount;
                    }
                }
            }

            float percentOldTrees = 100.0F * (float)oldTreeRecordCount / (float)(BIG6 - BNXT);
            if (percentOldTrees >= 50.0F)
            {
                stand.Warnings.TreesOld = true;
            }
            if (configuration.Variant.TreeModel == TreeModel.OrganonSwo)
            {
                if (configuration.IsEvenAge && stand.BreastHeightAgeInYears > 500)
                {
                    stand.Warnings.TreesOld = true;
                }
            }
            else if (configuration.Variant.TreeModel == TreeModel.OrganonNwo || configuration.Variant.TreeModel == TreeModel.OrganonSmc)
            {
                if (configuration.IsEvenAge && stand.BreastHeightAgeInYears > 120)
                {
                    stand.Warnings.TreesOld = true;
                }
            }
            else
            {
                if (configuration.IsEvenAge && stand.AgeInYears > 30)
                {
                    stand.Warnings.TreesOld = true;
                }
            }

            // BUGBUG: this is overcomplicated, should just check against maximum stand age using time step from OrganonVapabilities
            int standAgeBudgetAvailableAtNextTimeStep;
            if (configuration.IsEvenAge)
            {
                switch (configuration.Variant.TreeModel)
                {
                    case TreeModel.OrganonSwo:
                        standAgeBudgetAvailableAtNextTimeStep = 500 - stand.AgeInYears - 5;
                        break;
                    case TreeModel.OrganonNwo:
                    case TreeModel.OrganonSmc:
                        standAgeBudgetAvailableAtNextTimeStep = 120 - stand.AgeInYears - 5;
                        break;
                    case TreeModel.OrganonRap:
                        standAgeBudgetAvailableAtNextTimeStep = 30 - stand.AgeInYears - 1;
                        break;
                    default:
                        throw OrganonVariant.CreateUnhandledModelException(configuration.Variant.TreeModel);
                }
            }
            else
            {
                switch (configuration.Variant.TreeModel)
                {
                    case TreeModel.OrganonSwo:
                        standAgeBudgetAvailableAtNextTimeStep = 500 - (simulationStep + 1) * 5;
                        break;
                    case TreeModel.OrganonNwo:
                    case TreeModel.OrganonSmc:
                        standAgeBudgetAvailableAtNextTimeStep = 120 - (simulationStep + 1) * 5;
                        break;
                    case TreeModel.OrganonRap:
                        standAgeBudgetAvailableAtNextTimeStep = 30 - (simulationStep + 1) * 1;
                        break;
                    default:
                        throw OrganonVariant.CreateUnhandledModelException(configuration.Variant.TreeModel);
                }
            }

            if (standAgeBudgetAvailableAtNextTimeStep < 0)
            {
                stand.Warnings.TreesOld = true;
            }

            float B1 = -0.04484724F;
            foreach (Trees treesOfSpecies in stand.TreesBySpecies.Values)
            {
                FiaCode species = treesOfSpecies.Species;
                bool[] heightWarnings = null;
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    float B0;
                    switch (species)
                    {
                        case FiaCode.PseudotsugaMenziesii:
                            B0 = 19.04942539F;
                            break;
                        case FiaCode.TsugaHeterophylla:
                            if ((configuration.Variant.TreeModel == TreeModel.OrganonNwo) || (configuration.Variant.TreeModel == TreeModel.OrganonSmc))
                            {
                                B0 = 19.04942539F;
                            }
                            else if (configuration.Variant.TreeModel == TreeModel.OrganonRap)
                            {
                                B0 = 19.04942539F;
                            }
                            else
                            {
                                // BUGBUG Fortran code leaves B0 unitialized in Version.Swo case but also always treats hemlock as Douglas-fir
                                B0 = 19.04942539F;
                            }
                            break;
                        case FiaCode.AbiesConcolor:
                        case FiaCode.AbiesGrandis:
                            B0 = 16.26279948F;
                            break;
                        case FiaCode.PinusPonderosa:
                            B0 = 17.11482201F;
                            break;
                        case FiaCode.PinusLambertiana:
                            B0 = 14.29011403F;
                            break;
                        default:
                            B0 = 15.80319194F;
                            break;
                    }

                    float dbhInInches = treesOfSpecies.Dbh[treeIndex];
                    float potentialHeight = 4.5F + B0 * dbhInInches / (1.0F - B1 * dbhInInches);
                    float heightInFeet = treesOfSpecies.Height[treeIndex];
                    if (heightInFeet > potentialHeight)
                    {
                        if (heightWarnings == null)
                        {
                            heightWarnings = stand.TreeHeightWarningBySpecies.GetOrAdd(species, treesOfSpecies.Capacity);
                        }
                        heightWarnings[treeIndex] = true;
                    }
                }
            }
        }
    }
}
