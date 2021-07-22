﻿using Osu.Cof.Ferm.Organon;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Osu.Cof.Ferm.Heuristics
{
    public abstract class PrescriptionHeuristic : Heuristic<PrescriptionParameters>
    {
        protected readonly PrescriptionAllMoveLog? allMoveLog;
        protected readonly PrescriptionFirstInFirstOutMoveLog? lastNImprovingMovesLog;

        protected PrescriptionHeuristic(OrganonStand stand, PrescriptionParameters heuristicParameters, RunParameters runParameters, bool evaluatesAcrossRotationsAndDiscountRates)
            : base(stand, heuristicParameters, runParameters, evaluatesAcrossRotationsAndDiscountRates)
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
                int rotationLengthCapacity = 1;
                int financialScenarioCapacity = 1;
                if (evaluatesAcrossRotationsAndDiscountRates)
                {
                    rotationLengthCapacity = runParameters.RotationLengths.Count;
                    financialScenarioCapacity = runParameters.Financial.Count;
                }
                this.lastNImprovingMovesLog = new PrescriptionFirstInFirstOutMoveLog(rotationLengthCapacity, financialScenarioCapacity, Constant.DefaultSolutionPoolSize);
            }
        }

        protected abstract void EvaluateThinningPrescriptions(HeuristicResultPosition position, HeuristicResults results, HeuristicPerformanceCounters perfCounters);

        public override IHeuristicMoveLog? GetMoveLog()
        {
            if (this.HeuristicParameters.LogAllMoves)
            {
                Debug.Assert(this.lastNImprovingMovesLog == null);
                return this.allMoveLog;
            }
            else
            {
                Debug.Assert(this.allMoveLog == null);
                return this.lastNImprovingMovesLog;
            }
        }

        public override HeuristicPerformanceCounters Run(HeuristicResultPosition position, HeuristicResults results)
        {
            if (this.HeuristicParameters.MinimumConstructionGreediness != Constant.Grasp.FullyGreedyConstructionForMaximization)
            {
                throw new InvalidOperationException(nameof(this.HeuristicParameters.MinimumConstructionGreediness));
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
            if ((this.HeuristicParameters.DefaultIntensityStepSize < this.HeuristicParameters.MinimumIntensityStepSize) ||
                (this.HeuristicParameters.DefaultIntensityStepSize > this.HeuristicParameters.MaximumIntensityStepSize))
            {
                throw new InvalidOperationException(nameof(this.HeuristicParameters.DefaultIntensityStepSize));
            }
            if ((this.HeuristicParameters.MaximumIntensity < this.HeuristicParameters.MinimumIntensity) || (this.HeuristicParameters.MaximumIntensity > intensityUpperBound))
            {
                throw new InvalidOperationException(nameof(this.HeuristicParameters.MaximumIntensity));
            }
            if ((this.HeuristicParameters.MaximumIntensityStepSize > intensityUpperBound) || (this.HeuristicParameters.MaximumIntensityStepSize < this.HeuristicParameters.MinimumIntensityStepSize))
            {
                throw new InvalidOperationException(nameof(this.HeuristicParameters.MaximumIntensityStepSize));
            }
            if (this.HeuristicParameters.MinimumIntensity < 0.0F)
            {
                throw new InvalidOperationException(nameof(this.HeuristicParameters.MinimumIntensity));
            }
            if (this.HeuristicParameters.MinimumIntensityStepSize < 0.0F)
            {
                throw new InvalidOperationException(nameof(this.HeuristicParameters.MinimumIntensityStepSize));
            }

            if (this.CurrentTrajectory.Treatments.Harvests.Count > 3)
            {
                throw new NotSupportedException("Enumeration of more than three thinnings is not currently supported.");
            }

            Stopwatch stopwatch = new();
            stopwatch.Start();
            HeuristicPerformanceCounters perfCounters = new();

            // call solution construction to reuse cached growth model timesteps and thinning prescriptions where practical
            // Prescriptions and tree selection are overwritten in prescription enumeration, leaving only the minor benefit of reusing a few
            // timesteps. However, since construction is fully greedy, new coordinate descents can begin from the copied solution without
            // modifying it.
            HeuristicResultPosition constructionSourcePosition = position;
            if (position.RotationIndex == Constant.AllRotationPosition)
            {
                // for now, choose a initial position to search from within all rotations and all financial scenarios here
                // This can be moved into lower level code if needed.
                // Not valid to choose a rotation length which ends before the last thin. For now, the loop below is conservative and chooses
                // a rotation length one period longer than the last thinning. This is, at least for the moment, consistent with the filtering
                // logic in OptimizeCmdlet.
                int latestThinPeriod = Math.Max(Math.Max(results.FirstThinPeriods[position.FirstThinPeriodIndex], 
                                                         results.SecondThinPeriods[position.SecondThinPeriodIndex]),
                                                results.ThirdThinPeriods[position.ThirdThinPeriodIndex]);
                int minimumRotationIndex = -1;
                for (int rotationIndex = 0; rotationIndex < results.RotationLengths.Count; ++rotationIndex)
                {
                    if (results.RotationLengths[rotationIndex] > latestThinPeriod)
                    {
                        minimumRotationIndex = rotationIndex;
                        break;
                    }
                }
                if (minimumRotationIndex < 0)
                {
                    throw new InvalidOperationException("Couldn't find rotation length exceeding last thin period of " + latestThinPeriod + ".");
                }
                constructionSourcePosition = new(position)
                {
                    FinancialIndex = Constant.HeuristicDefault.FinancialIndex, // for now, assume searching from default index is most efficient
                    RotationIndex = minimumRotationIndex
                };
            }
            perfCounters.TreesRandomizedInConstruction += this.ConstructTreeSelection(constructionSourcePosition, results);

            this.EvaluateThinningPrescriptions(position, results, perfCounters);

            stopwatch.Stop();
            perfCounters.Duration = stopwatch.Elapsed;
            if (this.lastNImprovingMovesLog != null)
            {
                // since this.singleMoveLog.Add() is called only on improving moves it has no way of setting its count
                this.lastNImprovingMovesLog.LengthInMoves = perfCounters.MovesAccepted + perfCounters.MovesRejected;
            }
            return perfCounters;
        }
    }
}
