﻿using Osu.Cof.Ferm.Organon;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Osu.Cof.Ferm.Heuristics
{
    public class PrescriptionEnumeration : Heuristic<PrescriptionParameters>
    {
        private readonly PrescriptionAllMoveLog? allMoveLog;
        private readonly PrescriptionFirstInFirstOutMoveLog? highestFinancialValueMoveLog;

        public PrescriptionEnumeration(OrganonStand stand, PrescriptionParameters heuristicParameters, RunParameters runParameters)
            : base(stand, heuristicParameters, runParameters, evaluatesAcrossRotationsAndDiscountRates: true)
        {
            if (this.HeuristicParameters.LogAllMoves)
            {
                this.allMoveLog = new PrescriptionAllMoveLog();
            }
            else
            {
                // by default, store prescription intensities for only the highest LEV combination of thinning intensities found
                // This substantially reduces memory footprint in runs where many prescriptions are enumerated and helps to reduce the
                // size of objective log files. If needed, this can be changed to storing a larger number of prescriptions.
                this.highestFinancialValueMoveLog = new PrescriptionFirstInFirstOutMoveLog(runParameters.RotationLengths.Count, runParameters.Financial.Count, Constant.DefaultSolutionPoolSize);
            }
        }

        private void EnumerateThinningIntensities(ThinByPrescription thinPrescription, float percentIntensityOfPreviousThins, Action<float> evaluatePrescriptions, HeuristicPerformanceCounters perfCounters)
        {
            if ((percentIntensityOfPreviousThins < 0.0F) || (percentIntensityOfPreviousThins > 100.0F))
            {
                throw new ArgumentOutOfRangeException(nameof(percentIntensityOfPreviousThins));
            }

            float maximumPercentage = this.HeuristicParameters.MaximumIntensity;
            float minimumPercentage = this.HeuristicParameters.MinimumIntensity;
            switch (this.HeuristicParameters.Units)
            {
                case PrescriptionUnits.BasalAreaPerAcreRetained:
                    // obtain stand's basal area prior to thinning if it's not already available
                    if (this.CurrentTrajectory.DensityByPeriod[thinPrescription.Period - 1] == null)
                    {
                        // TODO: check for no conflicting other prescriptions
                        perfCounters.GrowthModelTimesteps += this.CurrentTrajectory.Simulate();
                    }
                    float basalAreaPerAcreBeforeThin = this.CurrentTrajectory.DensityByPeriod[thinPrescription.Period - 1].BasalAreaPerAcre;
                    if (maximumPercentage >= basalAreaPerAcreBeforeThin)
                    {
                        throw new InvalidOperationException(nameof(this.HeuristicParameters.MaximumIntensity));
                    }
                    // convert retained basal area to removed percentage
                    maximumPercentage = 100.0F * (1.0F - maximumPercentage / basalAreaPerAcreBeforeThin);
                    minimumPercentage = 100.0F * (1.0F - minimumPercentage / basalAreaPerAcreBeforeThin);
                    break;
                case PrescriptionUnits.StemPercentageRemoved:
                    // no changes needed
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled units {0}.", this.HeuristicParameters.Units));
            }

            float maximumAllowedPercentage = this.HeuristicParameters.FromAbovePercentageUpperLimit + this.HeuristicParameters.ProportionalPercentageUpperLimit + this.HeuristicParameters.FromBelowPercentageUpperLimit;
            if (maximumAllowedPercentage < minimumPercentage)
            {
                throw new InvalidOperationException(nameof(this.HeuristicParameters));
            }

            // adjust step size and minimum intensity for trees or basal area removed by earlier things
            // This is an approximate correction which can be made more detailed if needed.
            //   1) Both mortality and ingrowth may change tree counts between thins.
            //   2) Mortality and growth change basal area between thins.
            float previousIntensityMultiplier = 1.0F / (1.0F - 0.01F * percentIntensityOfPreviousThins);
            minimumPercentage = MathF.Min(previousIntensityMultiplier * minimumPercentage, maximumPercentage);
            float stepSize = MathF.Min(previousIntensityMultiplier * this.HeuristicParameters.DefaultIntensityStepSize, this.HeuristicParameters.MaximumIntensityStepSize);
            Debug.Assert(previousIntensityMultiplier >= 1.0F);
            Debug.Assert((stepSize > 0.0F) && (stepSize <= 100.0F));

            //int intensityStepsPerThinMethod = 1;
            //if (maximumPercentage > minimumPercentage)
            //{
            //    intensityStepsPerThinMethod += (int)(100.0F / (maximumPercentage - minimumPercentage));
            //}
            //this.AcceptedObjectiveFunctionByMove.Capacity = intensityStepsPerThinMethod * intensityStepsPerThinMethod * intensityStepsPerThinMethod;
            //this.CandidateObjectiveFunctionByMove.Capacity = this.AcceptedObjectiveFunctionByMove.Capacity;

            // This set of loops attempts to reactively set the
            // - proportional percentage based on the from above percentage
            // - from below percentage based on the proportional and from above percentages
            // in such a way that valid combinations will be found within the maximum and minimum intensities and percentage limits and
            // granularity specified by the step size. This is nontrivial and valid parameter combinations may exist which the current
            // code fails to locate.
            for (float fromAbovePercentage = 0.0F; fromAbovePercentage <= this.HeuristicParameters.FromAbovePercentageUpperLimit; fromAbovePercentage += stepSize)
            {
                float availableProportionalAndBelowPercentage = maximumPercentage - fromAbovePercentage;
                float maximumProportionalPercentage = MathF.Min(availableProportionalAndBelowPercentage, this.HeuristicParameters.ProportionalPercentageUpperLimit);
                float requiredProportionalPercentage = MathF.Max(minimumPercentage - fromAbovePercentage - this.HeuristicParameters.FromBelowPercentageUpperLimit, 0.0F);
                for (float proportionalPercentage = requiredProportionalPercentage; proportionalPercentage <= maximumProportionalPercentage; proportionalPercentage += stepSize)
                {
                    float availableBelowPercentage = availableProportionalAndBelowPercentage - proportionalPercentage;
                    float maximumFromBelowPercentage = MathF.Min(availableBelowPercentage, this.HeuristicParameters.FromBelowPercentageUpperLimit);
                    float requiredBelowPercentage = MathF.Max(minimumPercentage - proportionalPercentage - fromAbovePercentage, 0.0F);
                    for (float fromBelowPercentage = requiredBelowPercentage; fromBelowPercentage <= maximumFromBelowPercentage; fromBelowPercentage += stepSize)
                    {
                        Debug.Assert(fromBelowPercentage >= 0.0F);
                        float totalRelativeIntensityOfThisThin = fromAbovePercentage + proportionalPercentage + fromBelowPercentage;
                        Debug.Assert(totalRelativeIntensityOfThisThin >= minimumPercentage);
                        Debug.Assert(totalRelativeIntensityOfThisThin <= maximumPercentage);

                        thinPrescription.FromAbovePercentage = fromAbovePercentage;
                        thinPrescription.FromBelowPercentage = fromBelowPercentage;
                        thinPrescription.ProportionalPercentage = proportionalPercentage;

                        evaluatePrescriptions.Invoke(totalRelativeIntensityOfThisThin);
                    }
                }
            }
        }

        private void EvaluateCurrentPrescriptions(ThinByPrescription? firstThinPrescription, ThinByPrescription? secondThinPrescription, ThinByPrescription? thirdThinPrescription, IList<int> rotationLengths, HeuristicPerformanceCounters perfCounters)
        {
            // for now, assume execution with fixed thinning times and rotation lengths, meaning tree selections do not need to be moved between periods
            // this.CurrentTrajectory.DeselectAllTrees();
            perfCounters.GrowthModelTimesteps += this.CurrentTrajectory.Simulate();

            float[,] financialValueByDiscountRate = this.GetFinancialValueByRotationAndScenario(this.CurrentTrajectory);
            for (int rotationIndex = 0; rotationIndex < this.RunParameters.RotationLengths.Count; ++rotationIndex)
            {
                int endOfRotationPeriod = rotationLengths[rotationIndex];
                if (endOfRotationPeriod <= this.RunParameters.LastThinPeriod)
                {
                    continue; // not a valid rotation length because it doesn't include the last thinning scheduled
                }

                for (int financialIndex = 0; financialIndex < this.RunParameters.Financial.Count; ++financialIndex)
                {
                    float candidateFinancialValue = financialValueByDiscountRate[rotationIndex, financialIndex];
                    float acceptedFinancialValue = this.FinancialValue.GetHighestValue(rotationIndex, financialIndex);
                    if (candidateFinancialValue > acceptedFinancialValue)
                    {
                        // accept change of prescription if it improves upon the best solution
                        acceptedFinancialValue = candidateFinancialValue;
                        this.CopyTreeGrowthToBestTrajectory(this.CurrentTrajectory, rotationIndex, financialIndex);

                        if (this.highestFinancialValueMoveLog != null)
                        {
                            this.highestFinancialValueMoveLog.SetPrescription(rotationIndex, financialIndex, perfCounters.MovesAccepted + perfCounters.MovesRejected, firstThinPrescription, secondThinPrescription, thirdThinPrescription);
                        }

                        ++perfCounters.MovesAccepted;
                    }
                    else
                    {
                        ++perfCounters.MovesRejected;
                    }

                    this.FinancialValue.AddMove(rotationIndex, financialIndex, acceptedFinancialValue, candidateFinancialValue);
                }
            }

            if (this.allMoveLog != null)
            {
                this.allMoveLog.Add(firstThinPrescription, secondThinPrescription, thirdThinPrescription);
            }
        }

        public override string GetName()
        {
            return "Prescription";
        }

        public override IHeuristicMoveLog? GetMoveLog()
        {
            if (this.HeuristicParameters.LogAllMoves)
            {
                return this.allMoveLog;
            }
            else
            {
                return this.highestFinancialValueMoveLog;
            }
        }

        public override HeuristicPerformanceCounters Run(HeuristicResultPosition position, HeuristicResults results)
        {
            if (this.HeuristicParameters.ConstructionGreediness != Constant.Grasp.FullyGreedyConstructionForMaximization)
            {
                throw new InvalidOperationException(nameof(this.HeuristicParameters.ConstructionGreediness));
            }
            if (this.HeuristicParameters.InitialThinningProbability != 0.0F)
            {
                throw new InvalidOperationException(nameof(this.HeuristicParameters.InitialThinningProbability));
            }

            if ((this.HeuristicParameters.FromAbovePercentageUpperLimit < 0.0F) || (this.HeuristicParameters.FromAbovePercentageUpperLimit > 100.0F))
            {
                throw new InvalidOperationException(nameof(this.HeuristicParameters.FromAbovePercentageUpperLimit));
            }
            if ((this.HeuristicParameters.FromBelowPercentageUpperLimit < 0.0F) || (this.HeuristicParameters.FromBelowPercentageUpperLimit > 100.0F))
            {
                throw new InvalidOperationException(nameof(this.HeuristicParameters.FromBelowPercentageUpperLimit));
            }
            if ((this.HeuristicParameters.ProportionalPercentageUpperLimit < 0.0F) || (this.HeuristicParameters.ProportionalPercentageUpperLimit > 100.0F))
            {
                throw new InvalidOperationException(nameof(this.HeuristicParameters.ProportionalPercentageUpperLimit));
            }

            float intensityUpperBound = this.HeuristicParameters.Units switch
            {
                PrescriptionUnits.BasalAreaPerAcreRetained => 1000.0F,
                PrescriptionUnits.StemPercentageRemoved => 100.0F,
                _ => throw new NotSupportedException(String.Format("Unhandled units {0}.", this.HeuristicParameters.Units))
            };
            if ((this.HeuristicParameters.DefaultIntensityStepSize < 0.0F) || (this.HeuristicParameters.DefaultIntensityStepSize > intensityUpperBound))
            {
                throw new InvalidOperationException(nameof(this.HeuristicParameters.DefaultIntensityStepSize));
            }
            if ((this.HeuristicParameters.MaximumIntensity < 0.0F) || (this.HeuristicParameters.MaximumIntensity > intensityUpperBound))
            {
                throw new InvalidOperationException(nameof(this.HeuristicParameters.MaximumIntensity));
            }
            if ((this.HeuristicParameters.MinimumIntensity < 0.0F) || (this.HeuristicParameters.MinimumIntensity > intensityUpperBound))
            {
                throw new InvalidOperationException(nameof(this.HeuristicParameters.MinimumIntensity));
            }
            if (this.HeuristicParameters.MaximumIntensity < this.HeuristicParameters.MinimumIntensity)
            {
                throw new InvalidOperationException(nameof(this.HeuristicParameters.MinimumIntensity));
            }

            if (this.CurrentTrajectory.Treatments.Harvests.Count > 3)
            {
                throw new NotSupportedException("Enumeration of more than three thinnings is not currently supported.");
            }

            Stopwatch stopwatch = new();
            stopwatch.Start();
            HeuristicPerformanceCounters perfCounters = new();

            // can't currently copy from existing solutions as doing so results in a call to OrganonConfiguration.CopyFrom(), which overwrites treatments
            // no need to call ConstructTreeSelection() or EvaluateInitialSelection() as tree selection is done by the thinning prescriptions during
            // stand trajectory simulation
            IList<IHarvest> harvests = this.CurrentTrajectory.Treatments.Harvests;
            if (harvests.Count == 0)
            {
                // no thins: no intensities to enumerate so only a single growth model call to obtain a no action trajectory
                this.EvaluateCurrentPrescriptions(null, null, null, results.RotationLengths, perfCounters);
            }
            else
            {
                ThinByPrescription firstThinPrescription = (ThinByPrescription)this.CurrentTrajectory.Treatments.Harvests[0];

                if (harvests.Count == 1)
                {
                    // one thin
                    this.EnumerateThinningIntensities(firstThinPrescription, 0.0F, (float firstIntensity) =>
                    {
                        this.EvaluateCurrentPrescriptions(firstThinPrescription, null, null, results.RotationLengths, perfCounters);
                    }, perfCounters);
                }
                else
                {
                    ThinByPrescription secondThinPrescription = (ThinByPrescription)this.CurrentTrajectory.Treatments.Harvests[1];

                    if (harvests.Count == 2)
                    {
                        // two thins
                        this.EnumerateThinningIntensities(firstThinPrescription, 0.0F, (float firstIntensity) =>
                        {
                            this.EnumerateThinningIntensities(secondThinPrescription!, firstIntensity, (float secondIntensity) =>
                            {
                                this.EvaluateCurrentPrescriptions(firstThinPrescription, secondThinPrescription, null, results.RotationLengths, perfCounters);
                            }, perfCounters);
                        }, perfCounters);
                    }
                    else
                    {
                        // three thins
                        ThinByPrescription thirdThinPrescription = (ThinByPrescription)this.CurrentTrajectory.Treatments.Harvests[2];
                        this.EnumerateThinningIntensities(firstThinPrescription, 0.0F, (float firstIntensity) =>
                        {
                            this.EnumerateThinningIntensities(secondThinPrescription!, firstIntensity, (float secondIntensity) =>
                            {
                                float previousIntensity = firstIntensity + (100.0F - firstIntensity) * 0.01F * secondIntensity;
                                this.EnumerateThinningIntensities(thirdThinPrescription, previousIntensity, (float thirdIntensity) =>
                                {
                                    this.EvaluateCurrentPrescriptions(firstThinPrescription, secondThinPrescription, thirdThinPrescription, results.RotationLengths, perfCounters);
                                }, perfCounters);
                            }, perfCounters);
                        }, perfCounters);
                    }
                }
            }

            stopwatch.Stop();
            perfCounters.Duration = stopwatch.Elapsed;
            if (this.highestFinancialValueMoveLog != null)
            {
                // since this.singleMoveLog.Add() is called only on improving moves it has no way of setting its count
                this.highestFinancialValueMoveLog.LengthInMoves = perfCounters.MovesAccepted + perfCounters.MovesRejected;
            }
            return perfCounters;
        }
    }
}
