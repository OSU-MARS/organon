﻿using Mars.Seem.Extensions;
using Mars.Seem.Optimization;
using Mars.Seem.Tree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Mars.Seem.Silviculture
{
    // provide a non-template base class to allow PowerShell cmdlets to accept results from any heuristic as a parameter
    public class SilviculturalSpace
    {
        // indices are: parameter combination, first thin, second thin, third thin, rotation length, financial scenario
        // Nullability in multidimensional arrays does not appear supported as of Visual Studio 16.10.1. See remarks in constructor.
        private readonly SilviculturalCoordinateExploration[][][][][][] elements;

        public IList<SilviculturalCoordinate> CoordinatesEvaluated { get; private init; }

        public FinancialScenarios FinancialScenarios { get; private init; }
        public IList<int> FirstThinPeriods { get; private init; }
        public int ParameterCombinations { get; private init; }
        public IList<int> RotationLengths { get; private init; }
        public IList<int> SecondThinPeriods { get; private init; }
        public IList<int> ThirdThinPeriods { get; private init; }

        public SilviculturalSpace(IList<int> firstThinPeriods, IList<int> rotationLengths, FinancialScenarios financialScenarios)
            : this(1, firstThinPeriods, [ Constant.NoHarvestPeriod ], [ Constant.NoHarvestPeriod ], rotationLengths, financialScenarios, Constant.Default.SolutionPoolSize)
        {
            // forwards
        }

        public SilviculturalSpace(int parameterCombinationCount, IList<int> firstThinPeriods, IList<int> secondThinPeriods, IList<int> thirdThinPeriods, IList<int> rotationLengths, FinancialScenarios financialScenarios, int individualPoolSize)
        {
            this.elements = new SilviculturalCoordinateExploration[parameterCombinationCount][][][][][];

            this.CoordinatesEvaluated = [];
            this.FinancialScenarios = financialScenarios;
            this.FirstThinPeriods = firstThinPeriods;
            this.ParameterCombinations = parameterCombinationCount;
            this.RotationLengths = rotationLengths;
            this.SecondThinPeriods = secondThinPeriods;
            this.ThirdThinPeriods = thirdThinPeriods;

            // fill result arrays where applicable
            //   parameter array elements: never null
            //   first thin array elements: never null
            //   second and third thin and planning period thin array elements: maybe null
            //   results in discount rate array elements: never null
            // Jagged arrays which aren't needed are left null. Partly for the minor memory savings, mostly to increase the chances of
            // detecting storage of results to nonsensical locations or other indexing issues.
            for (int parameterIndex = 0; parameterIndex < parameterCombinationCount; ++parameterIndex)
            {
                SilviculturalCoordinateExploration[][][][][] parameterCombinationElements = new SilviculturalCoordinateExploration[this.FirstThinPeriods.Count][][][][];
                this.elements[parameterIndex] = parameterCombinationElements;

                for (int firstThinIndex = 0; firstThinIndex < this.FirstThinPeriods.Count; ++firstThinIndex)
                {
                    SilviculturalCoordinateExploration[][][][] firstThinElements = new SilviculturalCoordinateExploration[this.SecondThinPeriods.Count][][][];
                    parameterCombinationElements[firstThinIndex] = firstThinElements;

                    int firstThinPeriod = this.FirstThinPeriods[firstThinIndex];
                    for (int secondThinIndex = 0; secondThinIndex < this.SecondThinPeriods.Count; ++secondThinIndex)
                    {
                        int secondThinPeriod = this.SecondThinPeriods[secondThinIndex];
                        if ((secondThinPeriod == Constant.NoHarvestPeriod) || (secondThinPeriod > firstThinPeriod))
                        {
                            SilviculturalCoordinateExploration[][][] secondThinElements = new SilviculturalCoordinateExploration[this.ThirdThinPeriods.Count][][];
                            firstThinElements[secondThinIndex] = secondThinElements;

                            int lastOfFirstOrSecondThinPeriod = Math.Max(firstThinPeriod, secondThinPeriod);
                            for (int thirdThinIndex = 0; thirdThinIndex < this.ThirdThinPeriods.Count; ++thirdThinIndex)
                            {
                                int thirdThinPeriod = this.ThirdThinPeriods[thirdThinIndex];
                                if ((thirdThinPeriod == Constant.NoHarvestPeriod) || (thirdThinPeriod > secondThinPeriod))
                                {
                                    SilviculturalCoordinateExploration[][] thirdThinElements = new SilviculturalCoordinateExploration[this.RotationLengths.Count][];
                                    secondThinElements[thirdThinIndex] = thirdThinElements;

                                    int lastOfFirstSecondOrThirdThinPeriod = Math.Max(lastOfFirstOrSecondThinPeriod, thirdThinPeriod);
                                    for (int rotationIndex = 0; rotationIndex < this.RotationLengths.Count; ++rotationIndex)
                                    {
                                        int endOfRotationPeriodPeriod = this.RotationLengths[rotationIndex];
                                        if (endOfRotationPeriodPeriod > lastOfFirstSecondOrThirdThinPeriod)
                                        {
                                            SilviculturalCoordinateExploration[] rotationLengthElements = new SilviculturalCoordinateExploration[this.FinancialScenarios.Count];
                                            thirdThinElements[rotationIndex] = rotationLengthElements;

                                            for (int financialIndex = 0; financialIndex < this.FinancialScenarios.Count; ++financialIndex)
                                            {
                                                rotationLengthElements[financialIndex] = new SilviculturalCoordinateExploration(individualPoolSize);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public SilviculturalCoordinateExploration this[SilviculturalCoordinate coordinate]
        {
            get { return this[coordinate.ParameterIndex, coordinate.FirstThinPeriodIndex, coordinate.SecondThinPeriodIndex, coordinate.ThirdThinPeriodIndex, coordinate.RotationIndex, coordinate.FinancialIndex]; }
        }

        private SilviculturalCoordinateExploration this[int parameterIndex, int firstThinPeriodIndex, int secondThinPeriodIndex, int thirdThinPeriodIndex, int rotationIndex, int financialIndex]
        {
            get
            {
                Debug.Assert((parameterIndex >= 0) && (firstThinPeriodIndex >= 0) && (secondThinPeriodIndex >= 0) && (thirdThinPeriodIndex >= 0) && (rotationIndex >= 0) && (financialIndex >= 0));

                SilviculturalCoordinateExploration[][][][][] parameterElements = this.elements[parameterIndex];
                Debug.Assert(parameterElements != null, "Invalid parameter index " + parameterIndex + ".");
                SilviculturalCoordinateExploration[][][][] firstThinElements = parameterElements[firstThinPeriodIndex];
                Debug.Assert(firstThinElements != null, "Invalid first thin index " + firstThinPeriodIndex + ".");
                SilviculturalCoordinateExploration[][][] secondThinElements = firstThinElements[secondThinPeriodIndex];
                Debug.Assert(secondThinElements != null, "Invalid second thin index " + secondThinPeriodIndex + ".");
                SilviculturalCoordinateExploration[][] thirdThinElements = secondThinElements[thirdThinPeriodIndex];
                Debug.Assert(thirdThinElements != null, "Invalid third thin index " + thirdThinPeriodIndex + ".");
                SilviculturalCoordinateExploration[] rotationLengthElements = thirdThinElements[rotationIndex];
                Debug.Assert(rotationLengthElements != null, "Invalid rotation index " + rotationIndex + ".");
                SilviculturalCoordinateExploration element = rotationLengthElements[financialIndex];
                Debug.Assert(element != null);
                return element;
            }
            set
            {
                Debug.Assert((parameterIndex >= 0) && (firstThinPeriodIndex >= 0) && (secondThinPeriodIndex >= 0) && (thirdThinPeriodIndex >= 0) && (rotationIndex >= 0) && (financialIndex >= 0));

                SilviculturalCoordinateExploration[][][][][] parameterElements = this.elements[parameterIndex];
                Debug.Assert(parameterElements != null, "Invalid parameter index " + parameterIndex + ".");
                SilviculturalCoordinateExploration[][][][] firstThinElements = parameterElements[firstThinPeriodIndex];
                Debug.Assert(firstThinElements != null, "Invalid first thin index " + firstThinPeriodIndex + ".");
                SilviculturalCoordinateExploration[][][] secondThinElements = firstThinElements[secondThinPeriodIndex];
                Debug.Assert(secondThinElements != null, "Invalid second thin index " + secondThinPeriodIndex + ".");
                SilviculturalCoordinateExploration[][] thirdThinElements = secondThinElements[thirdThinPeriodIndex];
                Debug.Assert(thirdThinElements != null, "Invalid third thin index " + thirdThinPeriodIndex + ".");
                SilviculturalCoordinateExploration[] rotationLengthElements = thirdThinElements[rotationIndex];
                Debug.Assert(rotationLengthElements != null, "Invalid rotation index " + rotationIndex + ".");
                rotationLengthElements[financialIndex] = value; 
            }
        }

        // preferred to adding to PositionsEvaluated directly for argument checking
        public void AddEvaluatedPosition(SilviculturalCoordinate coordinate)
        {
            if ((coordinate.ParameterIndex < 0) || (coordinate.ParameterIndex >= this.ParameterCombinations) ||
                (coordinate.FirstThinPeriodIndex < 0) || (coordinate.FirstThinPeriodIndex >= this.FirstThinPeriods.Count) ||
                (coordinate.SecondThinPeriodIndex < 0) || (coordinate.SecondThinPeriodIndex >= this.SecondThinPeriods.Count) ||
                (coordinate.ThirdThinPeriodIndex < 0) || (coordinate.ThirdThinPeriodIndex >= this.ThirdThinPeriods.Count) ||
                (coordinate.RotationIndex < 0) || (coordinate.RotationIndex >= this.RotationLengths.Count) ||
                (coordinate.FinancialIndex < 0) || (coordinate.FinancialIndex >= this.FinancialScenarios.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(coordinate));
            }

            Debug.Assert(this.CoordinatesEvaluated.Contains(coordinate) == false);
            this.CoordinatesEvaluated.Add(coordinate);
        }

        public void AssimilateIntoCoordinate(StandTrajectory trajectory, float financialValue, SilviculturalCoordinate coordinate, PrescriptionPerformanceCounters perfCounters)
        {
            this.VerifyStandEntries(trajectory, coordinate);

            SilviculturalCoordinateExploration element = this[coordinate];
            element.Distribution.Add(financialValue, perfCounters);

            // additions to pools which haven't filled yet aren't informative to GRASP's reactive choice of α as there's no selection
            // pressure
            // One interpretation of this is the pool size sets the minimum amount of information needed to make decisions about the
            // value of solution diversity and objective function improvements.
            element.Pool.TryAddOrReplace(trajectory, financialValue);

            Debug.Assert((element.Pool.SolutionsInPool > 0) && (element.Pool.High.Trajectory != null) && (element.Pool.Low.Trajectory != null));
        }

        public StandTrajectory GetHighTrajectory(SilviculturalCoordinate coordinate)
        {
            SilviculturalPrescriptionPool prescriptions = this[coordinate].Pool;
            StandTrajectory? highTrajectory = prescriptions.High.Trajectory;
            if (highTrajectory == null)
            {
                throw new NotSupportedException("Precription pool at position (parameters: " + coordinate.ParameterIndex + ", financial: " + coordinate.FinancialIndex + ", first thin: " + coordinate.FirstThinPeriodIndex + ", second thin: " + coordinate.SecondThinPeriodIndex + ", third thin: " + coordinate.ThirdThinPeriodIndex + ", rotation: " + coordinate.RotationIndex + ") is missing a high trajectory.");
            }

            return highTrajectory;
        }

        public void GetPoolPerformanceCounters(out int solutionsCached, out int solutionsAccepted, out int solutionsRejected)
        {
            solutionsAccepted = 0;
            solutionsCached = 0;
            solutionsRejected = 0;

            for (int parameterIndex = 0; parameterIndex < this.ParameterCombinations; ++parameterIndex)
            {
                SilviculturalCoordinateExploration[][][][][] parameterCombinationElements = this.elements[parameterIndex];
                for (int firstThinIndex = 0; firstThinIndex < parameterCombinationElements.Length; ++firstThinIndex)
                {
                    SilviculturalCoordinateExploration[][][][] firstThinElements = parameterCombinationElements[firstThinIndex];
                    for (int secondThinIndex = 0; secondThinIndex < firstThinElements.Length; ++secondThinIndex)
                    {
                        SilviculturalCoordinateExploration[][][] secondThinElements = firstThinElements[secondThinIndex];
                        if (secondThinElements != null)
                        {
                            for (int thirdThinIndex = 0; thirdThinIndex < secondThinElements.Length; ++thirdThinIndex)
                            {
                                SilviculturalCoordinateExploration[][] thirdThinElements = secondThinElements[thirdThinIndex];
                                if (thirdThinElements != null)
                                {
                                    for (int rotationIndex = 0; rotationIndex < thirdThinElements.Length; ++rotationIndex)
                                    {
                                        SilviculturalCoordinateExploration[] rotationLengthElements = thirdThinElements[rotationIndex];
                                        if (rotationLengthElements != null)
                                        {
                                            for (int financialIndex = 0; financialIndex < rotationLengthElements.Length; ++financialIndex)
                                            {
                                                SilviculturalCoordinateExploration element = rotationLengthElements[financialIndex];
                                                solutionsAccepted += element.Pool.SolutionsAccepted;
                                                solutionsCached += element.Pool.SolutionsInPool;
                                                solutionsRejected += element.Pool.SolutionsRejected;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // searches among financial scenarios and rotation lengths for solutions assigned to the same set of heuristic parameters and thinnings
        // Looking across parameters would confound heuristic parameter tuning runs by allowing later parameter sets to access solutions obtained
        // by earlier runs. It's assumed parameter set evaluations should always be independent of each other and, therefore, searches are
        // restricted to be within thins, rotation lengths, and financial scenarios.
        public bool TryGetSelfOrFindNearestNeighbor(SilviculturalCoordinate coordinate, [NotNullWhen(true)] out SilviculturalPrescriptionPool? neighborOrSelf, [NotNullWhen(true)] out SilviculturalCoordinate? neighborOrSelfPosition)
        {
            neighborOrSelf = null;
            neighborOrSelfPosition = null;

            SilviculturalCoordinateExploration[][][][][] parameterElements = this.elements[coordinate.ParameterIndex];
            Debug.Assert(parameterElements != null);

            // for now, only search within the same number of thinnings
            if (this.FirstThinPeriods[coordinate.FirstThinPeriodIndex] == Constant.NoHarvestPeriod)
            {
                // no thins
                Debug.Assert(this.SecondThinPeriods[coordinate.SecondThinPeriodIndex] == Constant.NoHarvestPeriod);
                Debug.Assert(this.ThirdThinPeriods[coordinate.ThirdThinPeriodIndex] == Constant.NoHarvestPeriod);
                SilviculturalCoordinateExploration[][][][] firstThinElements = parameterElements[coordinate.FirstThinPeriodIndex];
                Debug.Assert(firstThinElements != null);
                SilviculturalCoordinateExploration[][][] secondThinElements = firstThinElements[coordinate.SecondThinPeriodIndex];
                Debug.Assert(secondThinElements != null);
                SilviculturalCoordinateExploration[][] thirdThinElements = secondThinElements[coordinate.ThirdThinPeriodIndex];

                if (SilviculturalSpace.TryGetSelfOrFindNeighborWithMatchingThinnings(thirdThinElements, coordinate, out neighborOrSelf, out neighborOrSelfPosition))
                {
                    return true;
                }
            }
            else if (this.SecondThinPeriods[coordinate.SecondThinPeriodIndex] == Constant.NoHarvestPeriod)
            {
                // one thin
                BreadthFirstEnumerator<SilviculturalCoordinateExploration[][][][]> thinEnumerator = new(parameterElements, coordinate.FirstThinPeriodIndex);
                while (thinEnumerator.MoveNext())
                {
                    // for now, exclude positions with different numbers of thins from neighborhood
                    if (this.FirstThinPeriods[thinEnumerator.Index] == Constant.NoHarvestPeriod)
                    {
                        continue;
                    }
                    Debug.Assert(this.ThirdThinPeriods[coordinate.ThirdThinPeriodIndex] == Constant.NoHarvestPeriod);

                    // position also has one thin, so search among rotation lengths and financial scenarios
                    SilviculturalCoordinateExploration[][][] secondThinElements = thinEnumerator.Current[coordinate.SecondThinPeriodIndex];
                    Debug.Assert(secondThinElements != null);
                    SilviculturalCoordinateExploration[][] thirdThinElements = secondThinElements[coordinate.ThirdThinPeriodIndex];
                    if (SilviculturalSpace.TryGetSelfOrFindNeighborWithMatchingThinnings(thirdThinElements, coordinate, out neighborOrSelf, out neighborOrSelfPosition))
                    {
                        return true;
                    }
                }
            }
            else if (this.ThirdThinPeriods[coordinate.ThirdThinPeriodIndex] == Constant.NoHarvestPeriod)
            {
                // two thins
                BreadthFirstEnumerator2D<SilviculturalCoordinateExploration[][][]> thinEnumerator = new(parameterElements, coordinate.FirstThinPeriodIndex, coordinate.SecondThinPeriodIndex);
                while (thinEnumerator.MoveNext())
                {
                    // for now, exclude positions with different numbers of thins from neighborhood
                    if ((this.FirstThinPeriods[thinEnumerator.IndexX] == Constant.NoHarvestPeriod) ||
                        (this.SecondThinPeriods[thinEnumerator.IndexY] == Constant.NoHarvestPeriod))
                    {
                        continue;
                    }
                    Debug.Assert(this.ThirdThinPeriods[coordinate.ThirdThinPeriodIndex] == Constant.NoHarvestPeriod);

                    // position also has two thins, so search among rotation lengths and financial scenarios
                    SilviculturalCoordinateExploration[][] thirdThinElements = thinEnumerator.Current[coordinate.ThirdThinPeriodIndex];
                    if (SilviculturalSpace.TryGetSelfOrFindNeighborWithMatchingThinnings(thirdThinElements, coordinate, out neighborOrSelf, out neighborOrSelfPosition))
                    {
                        return true;
                    }
                }
            }
            else
            {
                // three thins
                BreadthFirstEnumerator3D<SilviculturalCoordinateExploration[][]> thinEnumerator = new(parameterElements, coordinate.FirstThinPeriodIndex, coordinate.SecondThinPeriodIndex, coordinate.ThirdThinPeriodIndex);
                while (thinEnumerator.MoveNext())
                {
                    // for now, exclude positions with different numbers of thins from neighborhood
                    if ((this.FirstThinPeriods[thinEnumerator.IndexX] == Constant.NoHarvestPeriod) ||
                        (this.SecondThinPeriods[thinEnumerator.IndexY] == Constant.NoHarvestPeriod) ||
                        (this.ThirdThinPeriods[thinEnumerator.IndexZ] == Constant.NoHarvestPeriod))
                    {
                        continue;
                    }

                    // position also has three thins, so search among rotation lengths and financial scenarios
                    if (SilviculturalSpace.TryGetSelfOrFindNeighborWithMatchingThinnings(thinEnumerator.Current, coordinate, out neighborOrSelf, out neighborOrSelfPosition))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryGetSelfOrFindNeighborWithMatchingThinnings(SilviculturalCoordinateExploration[][] thirdThinElements, SilviculturalCoordinate coordinate, [NotNullWhen(true)] out SilviculturalPrescriptionPool? neighborOrSelf, [NotNullWhen(true)] out SilviculturalCoordinate? neighborOrSelfPosition)
        {
            neighborOrSelf = null;
            neighborOrSelfPosition = null;

            Debug.Assert((thirdThinElements != null) && (thirdThinElements.Length > 0));

            // for now, assume solutions at different rotation lengths are closer to each other than solutions at different financial indices
            // This is likely true for discount rate sweeps. It's likely false for financial uncertainty sweeps.
            BreadthFirstEnumerator2D<SilviculturalCoordinateExploration> rotationAndFinancialEnumerator = new(thirdThinElements, coordinate.RotationIndex, coordinate.FinancialIndex, BreadthFirstEnumerator2D.XFirst);
            while (rotationAndFinancialEnumerator.MoveNext())
            {
                neighborOrSelf = rotationAndFinancialEnumerator.Current.Pool;
                if (neighborOrSelf.SolutionsInPool > 0)
                {
                    neighborOrSelfPosition = new(coordinate)
                    {
                        RotationIndex = rotationAndFinancialEnumerator.IndexX,
                        FinancialIndex = rotationAndFinancialEnumerator.IndexY
                    };
                    return true;
                }
            }

            return false;
        }

        public void VerifyStandEntries(StandTrajectory trajectory, SilviculturalCoordinate coordinate)
        {
            // check heuristic's stand entries match the position it's being assigned to
            // If coordinate has negative period indices then it's incompletely initialized by the caller and an exception is expected.
            int firstThinPeriod = this.FirstThinPeriods[coordinate.FirstThinPeriodIndex];
            int secondThinPeriod = this.SecondThinPeriods[coordinate.SecondThinPeriodIndex];
            int thirdThinPeriod = this.ThirdThinPeriods[coordinate.ThirdThinPeriodIndex];
            int rotationLength = this.RotationLengths[coordinate.RotationIndex];
            if ((trajectory.GetFirstThinPeriod() != firstThinPeriod) ||
                (trajectory.GetSecondThinPeriod() != secondThinPeriod) ||
                (trajectory.GetThirdThinPeriod() != thirdThinPeriod) ||
                (trajectory.PlanningPeriods < rotationLength))
            {
                throw new ArgumentOutOfRangeException(nameof(trajectory), "Heuristic's stand entries do not match position. Heuristic versus position: first thin period " + trajectory.GetFirstThinPeriod() + " versus " + firstThinPeriod + ", second thin " + trajectory.GetSecondThinPeriod() + " versus " + secondThinPeriod + ", third thin " + trajectory.GetThirdThinPeriod() + " versus " + thirdThinPeriod + ", planning periods " + trajectory.PlanningPeriods + " versus " + rotationLength + ".");
            }
        }
    }
}
