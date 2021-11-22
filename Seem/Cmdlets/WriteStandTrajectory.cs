﻿using Osu.Cof.Ferm.Heuristics;
using Osu.Cof.Ferm.Organon;
using Osu.Cof.Ferm.Silviculture;
using Osu.Cof.Ferm.Tree;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management.Automation;
using System.Text;

namespace Osu.Cof.Ferm.Cmdlets
{
    [Cmdlet(VerbsCommunications.Write, "StandTrajectory")]
    public class WriteStandTrajectory : WriteHeuristicResultsOrStandTrajectoriesCmdlet
    {
        [Parameter]
        [ValidateRange(0.1F, 100.0F)]
        public float DiameterClassSize { get; set; } // cm

        [Parameter]
        [ValidateRange(1.0F, 1000.0F)]
        public float MaximumDiameter { get; set; } // cm

        public WriteStandTrajectory()
        {
            this.DiameterClassSize = Constant.Bucking.DiameterClassSizeInCentimeters;
            this.MaximumDiameter = Constant.Bucking.DefaultMaximumFinalHarvestDiameterInCentimeters;
        }

        protected override void ProcessRecord()
        {
            // this.DiameterClassSize and MaximumDiameter are checked by PowerShell
            this.ValidateParameters();

            using StreamWriter writer = this.GetWriter();

            // header
            if (this.ShouldWriteHeader())
            {
                HeuristicParameters? heuristicParameters = null;
                if (this.Results != null)
                {
                    heuristicParameters = WriteCmdlet.GetFirstHeuristicParameters(this.Results);
                }
                else if(this.Trajectories![0].Heuristic != null)
                {
                    heuristicParameters = this.Trajectories[0].Heuristic!.GetParameters();
                }

                writer.WriteLine(WriteCmdlet.GetHeuristicAndPositionCsvHeader(heuristicParameters) + "," +
                    "standAge,TPH,QMD,Htop,BA,SDI,liveTreeBiomass,SPH,snagQmd,standingCmh,standingMbfj,thinCmh,thinMbfh,BAremoved,BAintensity,TPHdecrease," + 
                    "NPV,LEV,thinLogs2S,thinLogs3S,thinLogs4S,thinCmh2S,thinCmh3S,thinCmh4S,thinMbfh2S,thinMbfh3S,thinMbfh4S,thinPond2S,thinPond3S,thinPond4S," + 
                    "thinWheeledHarvesterForwarderCost,thinTaskCost,thinWheeledHarvesterPMh,thinChainsawPMhWithWheeledHarvester,thinForwarderPMh,thinForwardedWeight,thinWheeledHarvesterProductivity,thinForwarderProductivity," +
                    "standingLogs2S,standingLogs3S,standingLogs4S,standingCmh2S,standingCmh3S,standingCmh4S,standingMbfh2S,standingMbfh3S,standingMbfh4S,regenPond2S,regenPond3S,regenPond4S," +
                    "regenMinCostSystem,regenFellerBuncherGrappleSwingYarderCost,regenFellerBuncherGrappleYoaderCost,regenTrackedHarvesterGrappleSwingYarderCost,regenTrackedHarvesterGrappleYoaderCost,regenWheeledHarvesterGrappleSwingYarderCost,regenWheeledHarvesterGrappleYoaderCost,regenTaskCost," +
                    "regenFellerBuncherPMh,regenTrackedHarvesterPMh,regenWheeledHarvesterPMh,regenChainsawPMhWithFellerBuncherAndGrappleSwingYarder,regenChainsawPMhWithFellerBuncherAndGrappleYoader,regenChainsawPMhWithTrackedHarvester,regenChainsawPMhWithWheeledHarvester,regenGrappleSwingYarderPMhPerHectare,regenGrappleYoaderPMhPerHectare,regenProcessorPMhWithGrappleSwingYarder,regenProcessorPMhWithGrappleYoader,regenLoadedWeight," +
                    "regenFellerBuncherProductivity,regenTrackedHarvesterProductivity,regenWheeledHarvesterProductivity,regenGrappleSwingYarderProductivity,regenGrappleYoaderProductivity,regenProcessorProductivityWithGrappleSwingYarder,regenProcessorProductivityWithGrappleYoader");
            }

            // rows for periods
            FinancialScenarios financialScenarios = this.Results != null ? this.Results.FinancialScenarios : this.Financial;
            long maxFileSizeInBytes = this.GetMaxFileSizeInBytes();
            int maxPositionIndex = this.GetMaxPositionIndex();
            for (int positionIndex = 0; positionIndex < maxPositionIndex; ++positionIndex)
            {
                OrganonStandTrajectory highTrajectory = this.GetHighestTrajectoryAndLinePrefix(positionIndex, out StringBuilder linePrefix, out int endOfRotationPeriod, out int financialIndex);
                Units trajectoryUnits = highTrajectory.GetUnits();
                if (trajectoryUnits != Units.English)
                {
                    throw new NotSupportedException("Expected Organon stand trajectory with English Units.");
                }
                highTrajectory.GetMerchantableVolumes(out StandMerchantableVolume standingVolume, out StandMerchantableVolume thinVolume);

                SnagDownLogTable snagsAndDownLogs = new(highTrajectory, this.MaximumDiameter, this.DiameterClassSize);

                float totalThinNetPresentValue = 0.0F;
                for (int period = 0; period <= endOfRotationPeriod; ++period)
                {
                    // get density and volumes
                    float basalAreaRemoved = Constant.AcresPerHectare * Constant.MetersPerFoot * Constant.MetersPerFoot * highTrajectory.Treatments.BasalAreaThinnedByPeriod[period]; // m²/acre
                    float basalAreaIntensity = 0.0F;
                    if (period > 0)
                    {
                        OrganonStandDensity? previousDensity = highTrajectory.DensityByPeriod[period - 1];
                        Debug.Assert(previousDensity != null, "Already checked in previous iteration of loop.");
                        basalAreaIntensity = basalAreaRemoved / previousDensity.BasalAreaPerAcre;
                    }
                    float thinVolumeScribner = thinVolume.GetScribnerTotal(period); // MBF/ha
                    Debug.Assert((thinVolumeScribner == 0.0F && basalAreaRemoved == 0.0F) || (thinVolumeScribner > 0.0F && basalAreaRemoved > 0.0F));

                    OrganonStandDensity? currentDensity = highTrajectory.DensityByPeriod[period];
                    if (currentDensity == null)
                    {
                        throw new ParameterOutOfRangeException(null, "Stand density information is missing for period " + period + ". Did the heuristic perform at least one fully simulated move?");
                    }

                    float treesPerAcreDecrease = 0.0F;
                    if (period > 0)
                    {
                        OrganonStandDensity? previousDensity = highTrajectory.DensityByPeriod[period - 1];
                        Debug.Assert(previousDensity != null, "Already checked in if clause above.");
                        treesPerAcreDecrease = 1.0F - currentDensity.TreesPerAcre / previousDensity.TreesPerAcre;
                    }

                    OrganonStand stand = highTrajectory.StandByPeriod[period] ?? throw new NotSupportedException("Stand information missing for period " + period + ".");
                    float quadraticMeanDiameterInCm = stand.GetQuadraticMeanDiameterInCentimeters();
                    float topHeightInM = stand.GetTopHeightInMeters();
                    // 1/(10 in * 2.54 cm/in) = 0.03937008
                    float reinekeStandDensityIndex = Constant.AcresPerHectare * currentDensity.TreesPerAcre * MathF.Pow(0.03937008F * quadraticMeanDiameterInCm, Constant.ReinekeExponent);

                    float treesPerHectare = Constant.AcresPerHectare * currentDensity.TreesPerAcre;
                    float basalAreaPerHectare = Constant.AcresPerHectare * Constant.MetersPerFoot * Constant.MetersPerFoot * currentDensity.BasalAreaPerAcre;
                    float treesPerHectareDecrease = Constant.AcresPerHectare * treesPerAcreDecrease;

                    // NPV and LEV
                    CutToLengthHarvest thinFinancialValue = period == 0 ? new() : financialScenarios.GetNetPresentThinningValue(highTrajectory, financialIndex, period);
                    totalThinNetPresentValue += thinFinancialValue.NetPresentValuePerHa;
                    LongLogHarvest regenFinancialValue = financialScenarios.GetNetPresentRegenerationHarvestValue(highTrajectory, financialIndex, period);
                    float reforestationNetPresentValue = financialScenarios.GetNetPresentReforestationValue(financialIndex, highTrajectory.PlantingDensityInTreesPerHectare);
                    regenFinancialValue.NetPresentValuePerHa += reforestationNetPresentValue;
                    regenFinancialValue.TaskCostPerHa -= reforestationNetPresentValue;
                    float periodNetPresentValue = totalThinNetPresentValue + regenFinancialValue.NetPresentValuePerHa;

                    float presentToFutureConversionFactor = financialScenarios.GetAppreciationFactor(financialIndex, highTrajectory.GetEndOfPeriodAge(endOfRotationPeriod));
                    float landExpectationValue = presentToFutureConversionFactor * periodNetPresentValue / (presentToFutureConversionFactor - 1.0F);

                    // pond value by grade (net of forest products harvest tax)
                    float pondValue2Saw = regenFinancialValue.PondValue2SawPerHa + thinFinancialValue.PondValue2SawPerHa;
                    float pondValue3Saw = regenFinancialValue.PondValue3SawPerHa + thinFinancialValue.PondValue3SawPerHa;
                    float pondValue4Saw = regenFinancialValue.PondValue4SawPerHa + thinFinancialValue.PondValue4SawPerHa;

                    // biomass
                    float liveBiomass = 0.001F * stand.GetLiveBiomass(); // Mg/ha

                    writer.WriteLine(linePrefix + "," +
                                     stand.AgeInYears.ToString(CultureInfo.InvariantCulture) + "," +
                                     treesPerHectare.ToString("0.0", CultureInfo.InvariantCulture) + "," +
                                     quadraticMeanDiameterInCm.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     topHeightInM.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     basalAreaPerHectare.ToString("0.0", CultureInfo.InvariantCulture) + "," +
                                     reinekeStandDensityIndex.ToString("0.0", CultureInfo.InvariantCulture) + "," +
                                     liveBiomass.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     snagsAndDownLogs.SnagsPerHectareByPeriod[period].ToString("0.0", CultureInfo.InvariantCulture) + "," +
                                     snagsAndDownLogs.SnagQmdInCentimetersByPeriod[period].ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     standingVolume.GetCubicTotal(period).ToString("0.000", CultureInfo.InvariantCulture) + "," +  // m³/ha
                                     standingVolume.GetScribnerTotal(period).ToString("0.000", CultureInfo.InvariantCulture) + "," +  // MBF/ha
                                     thinVolume.GetCubicTotal(period).ToString("0.000", CultureInfo.InvariantCulture) + "," +  // m³/ha
                                     thinVolumeScribner.ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     basalAreaRemoved.ToString("0.0", CultureInfo.InvariantCulture) + "," +
                                     basalAreaIntensity.ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     treesPerHectareDecrease.ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     periodNetPresentValue.ToString("0", CultureInfo.InvariantCulture) + "," +
                                     landExpectationValue.ToString("0", CultureInfo.InvariantCulture) + "," +
                                     thinVolume.Logs2Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     thinVolume.Logs3Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     thinVolume.Logs4Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     thinVolume.Cubic2Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     thinVolume.Cubic3Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     thinVolume.Cubic4Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     thinVolume.Scribner2Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     thinVolume.Scribner3Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     thinVolume.Scribner4Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     thinFinancialValue.PondValue2SawPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     thinFinancialValue.PondValue3SawPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     thinFinancialValue.PondValue4SawPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     thinFinancialValue.MinimumSystemCostPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     thinFinancialValue.TaskCostPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     thinFinancialValue.WheeledHarvesterPMhPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     thinFinancialValue.ChainsawPMhPerHaWithWheeledHarvester.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     thinFinancialValue.ForwarderPMhPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     thinFinancialValue.ForwardedWeightPeHa.ToString("0", CultureInfo.InvariantCulture) + "," +
                                     thinFinancialValue.Productivity.WheeledHarvester.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     thinFinancialValue.Productivity.Forwarder.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     standingVolume.Logs2Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     standingVolume.Logs3Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     standingVolume.Logs4Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     standingVolume.Cubic2Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     standingVolume.Cubic3Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     standingVolume.Cubic4Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     standingVolume.Scribner2Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     standingVolume.Scribner3Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     standingVolume.Scribner4Saw[period].ToString("0.000", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.PondValue2SawPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.PondValue3SawPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.PondValue4SawPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.GetMinimumCostHarvestSystem() + "," +
                                     regenFinancialValue.FellerBuncherGrappleSwingYarderProcessorLoaderCostPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.FellerBuncherGrappleYoaderProcessorLoaderCostPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.TrackedHarvesterGrappleSwingYarderLoaderCostPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.TrackedHarvesterGrappleYoaderLoaderCostPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.WheeledHarvesterGrappleSwingYarderLoaderCostPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.WheeledHarvesterGrappleYoaderLoaderCostPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.TaskCostPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.FellerBuncherPMhPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.TrackedHarvesterPMhPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.WheeledHarvesterPMhPerHa.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.ChainsawPMhPerHaWithFellerBuncherAndGrappleSwingYarder.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.ChainsawPMhPerHaWithFellerBuncherAndGrappleYoader.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.ChainsawPMhPerHaWithTrackedHarvester.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.ChainsawPMhPerHaWithWheeledHarvester.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.GrappleSwingYarderPMhPerHectare.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.GrappleYoaderPMhPerHectare.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.ProcessorPMhPerHaWithGrappleSwingYarder.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.ProcessorPMhPerHaWithGrappleYoader.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.LoadedWeightPerHa.ToString("0", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.Productivity.FellerBuncher.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.Productivity.TrackedHarvester.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.Productivity.WheeledHarvester.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.Productivity.GrappleSwingYarder.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.Productivity.GrappleYoader.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.Productivity.ProcessorWithGrappleSwingYarder.ToString("0.00", CultureInfo.InvariantCulture) + "," +
                                     regenFinancialValue.Productivity.ProcessorWithGrappleYoader.ToString("0.00", CultureInfo.InvariantCulture));
                }

                if (writer.BaseStream.Length > maxFileSizeInBytes)
                {
                    this.WriteWarning("Write-StandTrajectory: File size limit of " + this.LimitGB.ToString("0.00") + " GB exceeded.");
                    break;
                }
            }
        }
    }
}
