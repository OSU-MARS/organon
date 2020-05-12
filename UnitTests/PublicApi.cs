﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Osu.Cof.Ferm.Data;
using Osu.Cof.Ferm.Heuristics;
using Osu.Cof.Ferm.Organon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Osu.Cof.Ferm.Test
{
    [TestClass]
    public class PublicApi : OrganonTest
    {
        public TestContext TestContext { get; set; }

        private NelderPlot GetNelder()
        {
            string plotFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OSU", "Organon", "Nelder20.xlsx");
            return new NelderPlot(plotFilePath, "1");
        }

        [TestMethod]
        public void HuffmanPeakNobleFir()
        {
            string plotFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OSU", "Organon", "HPNF.xlsx");
            PspStand huffmanPeak = new PspStand(plotFilePath, "HPNF", 0.2F);
            OrganonVariant variant = new OrganonVariantSwo(); // SWO allows mapping ABAM -> ABGR and ABPR -> ABCO
            TestStand stand = huffmanPeak.ToStand(variant, 80.0F);
            int startYear = 1980;
            stand.WriteCompetitionAsCsv("HPNF initial competition.csv", variant, startYear);
            this.GrowPspStand(huffmanPeak, stand, variant, startYear, 2015, Path.GetFileNameWithoutExtension(plotFilePath));

            TreeQuantiles measuredQuantiles = new TreeQuantiles(stand, huffmanPeak, startYear);
            using StreamWriter quantileWriter = measuredQuantiles.WriteToCsv("HPNF measured quantiles.csv", variant, startYear);
            foreach (int measurementYear in huffmanPeak.MeasurementYears)
            {
                if (measurementYear != startYear)
                {
                    measuredQuantiles = new TreeQuantiles(stand, huffmanPeak, measurementYear);
                    measuredQuantiles.WriteToCsv(quantileWriter, variant, measurementYear);
                }
            }
        }

        [TestMethod]
        public void NelderHero()
        {
            int thinningPeriod = 4;
            int planningPeriods = 9;
            int treeCount = 100;
            float minObjectiveFunction = 3.78F;
            #if DEBUG
            treeCount = 48;
            minObjectiveFunction = 2.20F;
            #endif

            NelderPlot nelder = this.GetNelder();
            OrganonConfiguration configuration = new OrganonConfiguration(new OrganonVariantNwo());
            configuration.Treatments.Harvests.Add(new ThinByHeuristicIndividualTreeSelection(thinningPeriod));
            OrganonStand stand = nelder.ToStand(130.0F, treeCount);

            Objective netPresentValue = new Objective()
            {
                IsNetPresentValue = true,
                VolumeUnits = VolumeUnits.ScribnerBoardFeetPerAcre
            };
            Hero hero = new Hero(stand, configuration, planningPeriods, netPresentValue)
            {
                Iterations = 10
            };
            hero.RandomizeSelections(TestConstant.Default.HarvestProbability);
            hero.Run();

            this.Verify(hero);
            Assert.IsTrue(hero.BestObjectiveFunction > minObjectiveFunction);
            Assert.IsTrue(hero.BestObjectiveFunction < 1.02F * minObjectiveFunction);
        }

        [TestMethod]
        public void NelderOtherHeuristics()
        {
            int thinningPeriod = 4;
            int planningPeriods = 9;
            int treeCount = 75;
            #if DEBUG
            treeCount = 25;
            #endif

            NelderPlot nelder = this.GetNelder();
            OrganonConfiguration configuration = this.CreateOrganonConfiguration(new OrganonVariantNwo());
            configuration.Treatments.Harvests.Add(new ThinByHeuristicIndividualTreeSelection(thinningPeriod));
            OrganonStand stand = nelder.ToStand(130.0F, treeCount);

            Objective netPresentValue = new Objective()
            {
                IsNetPresentValue = true,
                VolumeUnits = VolumeUnits.ScribnerBoardFeetPerAcre
            };
            Objective volume = new Objective();

            GeneticAlgorithm genetic = new GeneticAlgorithm(stand, configuration, planningPeriods, netPresentValue)
            {
                EndStandardDeviation = 0.001F, // US$ 1 NPV
                PopulationSize = 7,
                MaximumGenerations = 8,
            };
            genetic.Run();

            GreatDeluge deluge = new GreatDeluge(stand, configuration, planningPeriods, volume)
            {
                RainRate = 5,
                StopAfter = 10
            };
            deluge.RandomizeSelections(TestConstant.Default.HarvestProbability);
            deluge.Run();

            RandomGuessing random = new RandomGuessing(stand, configuration, planningPeriods, netPresentValue, 0.5F)
            {
                Iterations = 5
            };
            random.Run();

            RecordToRecordTravel recordTravel = new RecordToRecordTravel(stand, configuration, planningPeriods, netPresentValue)
            {
                StopAfter = 10
            };
            recordTravel.RandomizeSelections(TestConstant.Default.HarvestProbability);
            recordTravel.Run();

            SimulatedAnnealing annealer = new SimulatedAnnealing(stand, configuration, planningPeriods, volume)
            {
                Alpha = 0.9F
            };
            annealer.RandomizeSelections(TestConstant.Default.HarvestProbability);
            annealer.Run();

            TabuSearch tabu = new TabuSearch(stand, configuration, planningPeriods, netPresentValue)
            {
                Iterations = 5
            };
            tabu.RandomizeSelections(TestConstant.Default.HarvestProbability);
            tabu.Run();

            ThresholdAccepting thresholdAcceptor = new ThresholdAccepting(stand, configuration, planningPeriods, volume)
            {
                IterationsPerThreshold = 10
            };
            thresholdAcceptor.RandomizeSelections(TestConstant.Default.HarvestProbability);
            thresholdAcceptor.Run();

            // heuristics assigned to volume optimization
            this.Verify(deluge);
            this.Verify(annealer);
            this.Verify(thresholdAcceptor);

            // heuristics assigned to net present value optimization
            this.Verify(genetic);
            this.Verify(random);
            this.Verify(recordTravel);
            this.Verify(tabu);
        }

        [TestMethod]
        public void NelderTrajectory()
        {
            int lastPeriod = 9;

            NelderPlot nelder = this.GetNelder();
            OrganonConfiguration configuration = this.CreateOrganonConfiguration(new OrganonVariantNwo());
            OrganonStand stand = nelder.ToStand(130.0F);

            OrganonStandTrajectory unthinnedTrajectory = new OrganonStandTrajectory(stand, configuration, lastPeriod, VolumeUnits.CubicMetersPerHectare);
            unthinnedTrajectory.Simulate();

            int thinPeriod = 3;
            configuration.Treatments.Harvests.Add(new ThinByPrescription(thinPeriod, 20.0F, 5.0F));
            OrganonStandTrajectory thinnedTrajectory = new OrganonStandTrajectory(stand, configuration, lastPeriod, VolumeUnits.CubicMetersPerHectare);
            thinnedTrajectory.Simulate();

            // verify untihnned trajectory
            //                                             0       1       2       3       4       5       6       7       8       9
            float[] minimumUnthinnedVolume = new float[] { 103.0F, 199.0F, 310.0F, 417.0F, 513.0F, 598.0F, 671.0F, 734.0F, 789.0F, 838.0F }; // m³
            this.Verify(unthinnedTrajectory, minimumUnthinnedVolume, 0, lastPeriod, 0, 0, configuration.Variant.TimeStepInYears);

            // verify thinned trajectory
            //                                           0       1       2       3       4       5       6       7       8       9
            float[] minimumThinnedVolume = new float[] { 103.0F, 199.0F, 310.0F, 338.0F, 436.0F, 530.0F, 617.0F, 694.0F, 763.0F, 823.0F }; // m³ for 20+5% thin
            this.Verify(thinnedTrajectory, minimumThinnedVolume, thinPeriod, lastPeriod, 200, 400, configuration.Variant.TimeStepInYears);
            for (int periodIndex = 0; periodIndex < thinnedTrajectory.PlanningPeriods; ++periodIndex)
            {
                if (periodIndex == thinPeriod)
                {
                    Assert.IsTrue(thinnedTrajectory.BasalAreaRemoved[periodIndex] > 0.0F);
                    Assert.IsTrue(thinnedTrajectory.BasalAreaRemoved[periodIndex] < thinnedTrajectory.DensityByPeriod[periodIndex - 1].BasalAreaPerAcre); // assume <50% thin by volume
                    Assert.IsTrue(thinnedTrajectory.HarvestVolumesByPeriod[periodIndex] > 0.0F);
                    Assert.IsTrue(thinnedTrajectory.HarvestVolumesByPeriod[periodIndex] < minimumThinnedVolume[periodIndex]);
                }
                else if (periodIndex < thinPeriod)
                {
                    Assert.IsTrue(thinnedTrajectory.BasalAreaRemoved[periodIndex] == 0.0F);
                    Assert.IsTrue(thinnedTrajectory.HarvestVolumesByPeriod[periodIndex] == 0.0F);
                }
            }
        }

        [TestMethod]
        public void OrganonStandGrowthApi()
        {
            TestStand.WriteTreeHeader(this.TestContext);
            foreach (OrganonVariant variant in TestConstant.Variants)
            {
                OrganonConfiguration configuration = this.CreateOrganonConfiguration(variant);

                // check crown competition API
                TestStand stand = this.CreateDefaultStand(configuration);
                float crownCompetitionFactor = OrganonStandDensity.GetCrownCompetitionByHeight(variant, stand)[0];
                Assert.IsTrue(crownCompetitionFactor >= 0.0F);
                Assert.IsTrue(crownCompetitionFactor <= TestConstant.Maximum.CrownCompetitionFactor);
                this.Verify(ExpectedTreeChanges.NoDiameterOrHeightGrowth | ExpectedTreeChanges.NoDiameterOrHeightGrowth, stand, variant);

                // recalculate heights and crown ratios for all trees
                Dictionary<FiaCode, float[]> CALIB = configuration.CreateSpeciesCalibration();
                foreach (Trees treesOfSpecies in stand.TreesBySpecies.Values)
                {
                    OrganonGrowth.SetIngrowthHeightAndCrownRatio(variant, stand, treesOfSpecies, treesOfSpecies.Count, CALIB);
                }
                this.Verify(ExpectedTreeChanges.NoDiameterOrHeightGrowth | ExpectedTreeChanges.NoDiameterOrHeightGrowth, stand, variant);

                // run Organon growth simulation
                stand = this.CreateDefaultStand(configuration);
                if (configuration.IsEvenAge)
                {
                    // stand error if less than one year to grow to breast height
                    stand.AgeInYears = stand.BreastHeightAgeInYears + 2;
                }
                stand.SetQuantiles();
                stand.WriteTreesAsCsv(this.TestContext, variant, 0, false);

                TestStand initialStand = new TestStand(stand);
                TreeLifeAndDeath treeGrowth = new TreeLifeAndDeath();
                for (int simulationStep = 0; simulationStep < TestConstant.Default.SimulationCyclesToRun; ++simulationStep)
                {
                    OrganonGrowth.Grow(simulationStep, configuration, stand, CALIB);
                    treeGrowth.AccumulateGrowthAndMortality(stand);
                    stand.SetSdiMax(configuration);
                    this.Verify(ExpectedTreeChanges.DiameterGrowth | ExpectedTreeChanges.HeightGrowth, OrganonWarnings.LessThan50TreeRecords, stand, variant);

                    stand.WriteTreesAsCsv(this.TestContext, variant, variant.GetEndYear(simulationStep), false);
                }

                this.Verify(ExpectedTreeChanges.DiameterGrowth | ExpectedTreeChanges.HeightGrowth, treeGrowth, initialStand, stand);
                this.Verify(CALIB);
            }
        }

        [TestMethod]
        public void RS39()
        {
            string plotFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OSU", "Organon", "RS39 lower half.xlsx");
            PspStand rs39 = new PspStand(plotFilePath, "RS39 lower half", 0.154441F);
            OrganonVariant variant = new OrganonVariantNwo();
            TestStand stand = rs39.ToStand(variant, 105.0F);
            int startYear = 1992;
            stand.WriteCompetitionAsCsv("RS39 lower half initial competition.csv", variant, startYear);
            this.GrowPspStand(rs39, stand, variant, startYear, 2019, Path.GetFileNameWithoutExtension(plotFilePath));

            TreeQuantiles measuredQuantiles = new TreeQuantiles(stand, rs39, startYear);
            using StreamWriter quantileWriter = measuredQuantiles.WriteToCsv("RS39 lower half measured quantiles.csv", variant, startYear);
            foreach (int measurementYear in rs39.MeasurementYears)
            {
                if (measurementYear != startYear)
                {
                    measuredQuantiles = new TreeQuantiles(stand, rs39, measurementYear);
                    measuredQuantiles.WriteToCsv(quantileWriter, variant, measurementYear);
                }
            }
        }

        // basic growth model performance benchmark: 3 runs of hero with 300 trees
        [TestMethod]
        public void StandGrowthPerformance()
        {
            int thinningPeriod = 4;
            int planningPeriods = 9;
            int runs = 4; // 1 warmup run + measured runs
            int trees = 300;
            #if DEBUG
            runs = 1; // do only functional validation of test on DEBUG builds to reduce test execution time
            trees = 10;
            #endif

            string plotFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OSU", "Organon", "Nelder20.xlsx");
            NelderPlot nelder = new NelderPlot(plotFilePath, "1");
            OrganonConfiguration configuration = this.CreateOrganonConfiguration(new OrganonVariantNwo());
            configuration.Treatments.Harvests.Add(new ThinByHeuristicIndividualTreeSelection(thinningPeriod));
            OrganonStand stand = nelder.ToStand(130.0F, trees);

            Objective netPresentValue = new Objective()
            {
                IsNetPresentValue = true,
                VolumeUnits = VolumeUnits.ScribnerBoardFeetPerAcre
            };

            TimeSpan runtime = TimeSpan.Zero;
            for (int run = 0; run < runs; ++run)
            {
                // 3 runs * 300 trees = 900 growth simulations on i7-3770 (4th gen, Sandy Bridge)
                // .NET standard 2.0 + Math: 12.978s -> 69.3 sims/core-s       97.8% StandTrajectory.Simulate(): 89.5% Organon, 7.6% FIA Scribner volume
                // .NET standard 2.0 + MathF: 9.670s -> 93.1 sims/core-s  +34% 
                Hero hero = new Hero(stand, configuration, planningPeriods, netPresentValue)
                {
                    Iterations = 2
                };
                hero.RandomizeSelections(TestConstant.Default.HarvestProbability);
                if (run > 0)
                {
                    // skip first run as a warmup run
                    runtime += hero.Run();
                }
            }
            this.TestContext.WriteLine(runtime.TotalSeconds.ToString());
        }

        private void Verify(Heuristic heuristic)
        {
            // check objective functions
            double beginObjectiveFunction = heuristic.ObjectiveFunctionByMove.First();
            double endObjectiveFunction = heuristic.ObjectiveFunctionByMove.Last();
            double recalculatedBestObjectiveFunction = heuristic.GetObjectiveFunction(heuristic.BestTrajectory);
            double recalculatedEndObjectiveFunction = heuristic.GetObjectiveFunction(heuristic.CurrentTrajectory);

            double bestObjectiveFunctionRatio = heuristic.BestObjectiveFunction / recalculatedBestObjectiveFunction;
            double endObjectiveFunctionRatio = endObjectiveFunction / recalculatedEndObjectiveFunction;

            Assert.IsTrue(heuristic.BestObjectiveFunction >= 0.0F);
            Assert.IsTrue(heuristic.BestObjectiveFunction >= beginObjectiveFunction);
            Assert.IsTrue(heuristic.BestObjectiveFunction >= endObjectiveFunction);
            Assert.IsTrue(heuristic.ObjectiveFunctionByMove.Count >= 3);

            Assert.IsTrue(bestObjectiveFunctionRatio > 0.99999);
            Assert.IsTrue(bestObjectiveFunctionRatio < 1.00001);
            // currently, this check can fail for genetic algorithms when the best individual doesn't breed and disimprovement results
            // TODO: either change GA to guarantee best individual breeds or relax this check in the GA case
            Assert.IsTrue(endObjectiveFunctionRatio > 0.99999);
            Assert.IsTrue(endObjectiveFunctionRatio < 1.00001);

            // check harvest schedule
            int harvestPeriod = heuristic.BestTrajectory.HarvestPeriods - 1;
            foreach (KeyValuePair<FiaCode, int[]> selectionForSpecies in heuristic.BestTrajectory.IndividualTreeSelectionBySpecies)
            {
                int[] bestTreeSelection = selectionForSpecies.Value;
                int[] currentTreeSelection = heuristic.CurrentTrajectory.IndividualTreeSelectionBySpecies[selectionForSpecies.Key];
                Assert.IsTrue(bestTreeSelection.Length == currentTreeSelection.Length);
                for (int treeIndex = 0; treeIndex < bestTreeSelection.Length; ++treeIndex)
                {
                    Assert.IsTrue((bestTreeSelection[treeIndex] == 0) || (bestTreeSelection[treeIndex] == harvestPeriod));
                    Assert.IsTrue((currentTreeSelection[treeIndex] == 0) || (currentTreeSelection[treeIndex] == harvestPeriod));
                }
            }

            // check volumes
            Assert.IsTrue(harvestPeriod > 0);
            for (int periodIndex = 0; periodIndex < heuristic.BestTrajectory.PlanningPeriods; ++periodIndex)
            {
                if (periodIndex < harvestPeriod)
                {
                    // for now, harvest should occur only in indicated period
                    Assert.IsTrue(heuristic.BestTrajectory.BasalAreaRemoved[periodIndex] == 0.0F);
                    Assert.IsTrue(heuristic.BestTrajectory.HarvestVolumesByPeriod[periodIndex] == 0.0F);
                    Assert.IsTrue(heuristic.CurrentTrajectory.BasalAreaRemoved[periodIndex] == 0.0F);
                    Assert.IsTrue(heuristic.CurrentTrajectory.HarvestVolumesByPeriod[periodIndex] == 0.0F);
                }
                else if (periodIndex == harvestPeriod)
                {
                    Assert.IsTrue(heuristic.BestTrajectory.BasalAreaRemoved[periodIndex] >= 0.0F); // best selection with debug stand is no harvest
                    Assert.IsTrue(heuristic.BestTrajectory.BasalAreaRemoved[periodIndex] <= 200.0F);
                    Assert.IsTrue(heuristic.BestTrajectory.HarvestVolumesByPeriod[periodIndex] >= 0.0F);
                    Assert.IsTrue(heuristic.BestTrajectory.HarvestVolumesByPeriod[periodIndex] <= heuristic.BestTrajectory.StandingVolumeByPeriod[periodIndex - 1]);

                    Assert.IsTrue(heuristic.CurrentTrajectory.BasalAreaRemoved[periodIndex] >= 0.0F);
                    Assert.IsTrue(heuristic.CurrentTrajectory.BasalAreaRemoved[periodIndex] <= 200.0F);
                    Assert.IsTrue(heuristic.CurrentTrajectory.HarvestVolumesByPeriod[periodIndex] >= 0.0F);
                    Assert.IsTrue(heuristic.CurrentTrajectory.HarvestVolumesByPeriod[periodIndex] <= heuristic.CurrentTrajectory.StandingVolumeByPeriod[periodIndex - 1]);
                }
                // otherwise past end of harvest arrays

                Assert.IsTrue(heuristic.BestTrajectory.StandingVolumeByPeriod[periodIndex] > 0.0F);
                Assert.IsTrue(heuristic.CurrentTrajectory.StandingVolumeByPeriod[periodIndex] > 0.0F);
                if ((periodIndex > 1) && (periodIndex != harvestPeriod))
                {
                    // for now, assume monotonic increase in standing volumes except in harvest periods
                    Assert.IsTrue(heuristic.BestTrajectory.StandingVolumeByPeriod[periodIndex] > heuristic.BestTrajectory.StandingVolumeByPeriod[periodIndex - 1]);
                    Assert.IsTrue(heuristic.CurrentTrajectory.StandingVolumeByPeriod[periodIndex] > heuristic.CurrentTrajectory.StandingVolumeByPeriod[periodIndex - 1]);
                }
            }
        }

        private void Verify(OrganonStandTrajectory trajectory, float[] minimumVolume, int thinPeriod, int lastPeriod, int minTrees, int maxTrees, int timeStepInYears)
        {
            Assert.IsTrue(trajectory.BasalAreaRemoved.Length == thinPeriod + 1);
            Assert.IsTrue(trajectory.BasalAreaRemoved[0] == 0.0F);
            Assert.IsTrue(trajectory.HarvestPeriods == thinPeriod + 1); // BUGBUG: clean off by one semantic
            Assert.IsTrue(trajectory.HarvestVolumesByPeriod.Length == thinPeriod + 1);
            Assert.IsTrue(trajectory.HarvestVolumesByPeriod[0] == 0.0F);
            Assert.IsTrue(trajectory.IndividualTreeExpansionFactor > 0.0F);
            this.Verify(trajectory.IndividualTreeSelectionBySpecies, thinPeriod, minTrees, maxTrees);
            Assert.IsTrue(String.IsNullOrEmpty(trajectory.Name) == false);
            Assert.IsTrue(trajectory.PeriodLengthInYears == timeStepInYears);
            Assert.IsTrue(trajectory.PlanningPeriods == lastPeriod + 1); // BUGBUG: clean off by one semantic
            Assert.IsTrue(trajectory.VolumeUnits == VolumeUnits.CubicMetersPerHectare);

            float volumeTolerance = 1.01F;
            for (int periodIndex = 0; periodIndex < trajectory.PlanningPeriods; ++periodIndex)
            {
                Assert.IsTrue(trajectory.DensityByPeriod[periodIndex].BasalAreaPerAcre > 0.0F);
                Assert.IsTrue(trajectory.DensityByPeriod[periodIndex].BasalAreaPerAcre <= TestConstant.Maximum.TreeBasalAreaLarger);
                Assert.IsTrue(trajectory.StandingVolumeByPeriod[periodIndex] > minimumVolume[periodIndex]);
                Assert.IsTrue(trajectory.StandingVolumeByPeriod[periodIndex] < volumeTolerance * minimumVolume[periodIndex]);
                Assert.IsTrue(trajectory.StandByPeriod[periodIndex].Name.StartsWith(trajectory.Name));
            }
        }

        private void Verify(SortedDictionary<FiaCode, int[]> individualTreeSelectionBySpecies, int harvestPeriod, int minimumTreesSelected, int maximumTreesSelected)
        {
            int outOfRangeTrees = 0;
            int treesSelected = 0;
            foreach (int[] individualTreeSelection in individualTreeSelectionBySpecies.Values)
            {
                for (int treeIndex = 0; treeIndex < individualTreeSelection.Length; ++treeIndex)
                {
                    int treeSelection = individualTreeSelection[treeIndex];
                    bool isOutOfRange = (treeSelection != 0) && (treeSelection != harvestPeriod);
                    if (isOutOfRange)
                    {
                        ++outOfRangeTrees;
                    }

                    bool isSelected = treeSelection != 0;
                    if (isSelected)
                    {
                        ++treesSelected;
                    }
                }
            }

            Assert.IsTrue(outOfRangeTrees == 0);
            Assert.IsTrue(treesSelected >= minimumTreesSelected);
            Assert.IsTrue(treesSelected <= maximumTreesSelected);
        }

        [TestMethod]
        public void Volume()
        {
            int treeCount = 42;

            // TODO: TSHE, THPL, ...
            FiaVolume fiaVolume = new FiaVolume();
            OsuVolume osuVolume = new OsuVolume();
            Trees trees = new Trees(FiaCode.PseudotsugaMenziesii, treeCount, Units.English);
            float[] fiaMerchantableCubicFeetPerAcre = new float[treeCount];
            float[] fiaScribnerBoardFeetPerAcre = new float[treeCount];
            float[] osuMerchantableCubicMetersPerHectare = new float[treeCount];
            float merchantableCubicFeetPerAcre = 0.0F;
            float merchantableCubicMetersPerHectare = 0.0F;
            float totalCylinderCubicMeterVolumePerAcre = 0.0F;
            float totalScribnerBoardFeetPerAcre = 0.0F;
            for (int treeIndex = 0; treeIndex < treeCount; ++treeIndex)
            {
                // create trees with a range of expansion factors to catch errors in expansion factor management
                float treeRatio = (float)treeIndex / (float)treeCount;
                TreeRecord tree = new TreeRecord(treeIndex, trees.Species, (float)treeIndex, 1.0F - 0.75F * treeRatio, 0.6F + treeIndex);
                trees.Add(tree.Tag, tree.DbhInInches, tree.HeightInFeet, tree.CrownRatio, tree.LiveExpansionFactor);

                float dbhInMeters = TestConstant.MetersPerInch * tree.DbhInInches;
                float heightInMeters = Constant.MetersPerFoot * tree.HeightInFeet;
                float treeSizedCylinderCubicMeterVolumePerAcre = tree.LiveExpansionFactor * 0.25F * MathF.PI * dbhInMeters * dbhInMeters * heightInMeters;

                fiaMerchantableCubicFeetPerAcre[treeIndex] = tree.LiveExpansionFactor * fiaVolume.GetMerchantableCubicFeet(trees, treeIndex);
                merchantableCubicFeetPerAcre += fiaMerchantableCubicFeetPerAcre[treeIndex];
                fiaScribnerBoardFeetPerAcre[treeIndex] = tree.LiveExpansionFactor * fiaVolume.GetScribnerBoardFeet(trees, treeIndex);
                totalScribnerBoardFeetPerAcre += fiaScribnerBoardFeetPerAcre[treeIndex];

                osuMerchantableCubicMetersPerHectare[treeIndex] = Constant.HectaresPerAcre * tree.LiveExpansionFactor * osuVolume.GetCubicVolume(trees, treeIndex);
                merchantableCubicMetersPerHectare += osuMerchantableCubicMetersPerHectare[treeIndex];

                // taper coefficient should be in the vicinity of 0.3 for larger trees, but this is not well defined for small trees
                // Lower bound can be made more stringent if necessary.
                Assert.IsTrue(fiaMerchantableCubicFeetPerAcre[treeIndex] >= 0.0);
                Assert.IsTrue(fiaMerchantableCubicFeetPerAcre[treeIndex] <= 0.4 * Constant.CubicFeetPerCubicMeter * treeSizedCylinderCubicMeterVolumePerAcre);

                Assert.IsTrue(fiaScribnerBoardFeetPerAcre[treeIndex] >= 0.0);
                Assert.IsTrue(fiaScribnerBoardFeetPerAcre[treeIndex] <= 6.5 * 0.4 * Constant.CubicFeetPerCubicMeter * treeSizedCylinderCubicMeterVolumePerAcre);
                totalCylinderCubicMeterVolumePerAcre += treeSizedCylinderCubicMeterVolumePerAcre;
            }

            float totalCylinderCubicFeetVolumePerAcre = Constant.CubicFeetPerCubicMeter * totalCylinderCubicMeterVolumePerAcre;
            Assert.IsTrue(merchantableCubicFeetPerAcre >= 0.05 * totalCylinderCubicFeetVolumePerAcre);
            Assert.IsTrue(merchantableCubicFeetPerAcre <= 0.35 * totalCylinderCubicFeetVolumePerAcre);
            Assert.IsTrue(merchantableCubicFeetPerAcre >= 0.5 * Constant.AcresPerHectare * Constant.CubicFeetPerCubicMeter * merchantableCubicMetersPerHectare);

            Assert.IsTrue(merchantableCubicMetersPerHectare <= 0.35 * Constant.AcresPerHectare * totalCylinderCubicMeterVolumePerAcre);

            Assert.IsTrue(totalScribnerBoardFeetPerAcre >= 1.75 * 0.35 * totalCylinderCubicFeetVolumePerAcre);
            Assert.IsTrue(totalScribnerBoardFeetPerAcre <= 6.5 * 0.40 * totalCylinderCubicFeetVolumePerAcre);

            // check SIMD 128 result against scalar
            float totalScribnerBoardFeetPerAcre128 = fiaVolume.GetScribnerBoardFeetPerAcre(trees);
            float simdScalarScribnerDifference = totalScribnerBoardFeetPerAcre - totalScribnerBoardFeetPerAcre128;
            Assert.IsTrue(MathF.Abs(simdScalarScribnerDifference) < 0.004 * totalScribnerBoardFeetPerAcre);
        }

        [TestMethod]
        public void VolumePerformance()
        {
            int iterations = 1025; // 5 warmup run + measured runs
            int treeCount = 100 * 1000;
            #if DEBUG
            iterations = 25; // do only functional validation of test on DEBUG builds to reduce test execution time
            treeCount = 125;
            #endif

            Trees trees = new Trees(FiaCode.PseudotsugaMenziesii, treeCount, Units.English);
            float expansionFactor = 0.5F;
            for (int treeIndex = 0; treeIndex < treeCount; ++treeIndex)
            {
                float dbhInInches = (float)(treeIndex % 36 + 4);
                trees.Add(treeIndex, dbhInInches, 16.0F * MathF.Sqrt(dbhInInches) + 4.5F, 0.01F * (float)(treeIndex % 100), expansionFactor);
            }
            FiaVolume volume = new FiaVolume();

            Stopwatch runtime = new Stopwatch();
            float accumulatedBoardFeetPerAcre = 0.0F;
            for (int iteration = 0; iteration < iterations; ++iteration)
            {
                if (iteration > 20)
                {
                    // skip first few runs as warmup runs
                    runtime.Start();
                }

                // 1000 runs * 10,000 trees = 10M volumes on i7-3770
                // .NET standard 2.0 + MathF: 12.854s -> 686k trees/core-s
                //      RS616 pow() -> exp(): 13.254s -> 754k
                //              MathV scalar:  9.451s -> 1.06M
                //          reuse log10(DBH):  8.897s -> 1.12M
                //       aggressive inlining:  7.998s -> 1.20M
                //             internal loop:  7.785s -> 1.28M
                //            single species:  7.619s -> 1.31M
                //                   VEX 128:  2.063s -> 4.85M
                float standBoardFeetPerAcre = volume.GetScribnerBoardFeetPerAcre(trees);
                #if DEBUG
                Assert.IsTrue(standBoardFeetPerAcre > 35.0F * 1000.0F * expansionFactor);
                Assert.IsTrue(standBoardFeetPerAcre < 40.0F * 1000.0F * expansionFactor);
                #endif
                accumulatedBoardFeetPerAcre += standBoardFeetPerAcre;
            }
            runtime.Stop();

            this.TestContext.WriteLine(runtime.Elapsed.TotalSeconds.ToString());
        }
    }
}
