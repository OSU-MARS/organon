﻿using Osu.Cof.Organon.Heuristics;
using System;
using System.Management.Automation;

namespace Osu.Cof.Organon.Cmdlets
{
    [Cmdlet(VerbsCommon.Optimize, "SimulatedAnnealing")]
    public class OptimizeSimulatedAnnealing : OptimizeCmdlet
    {
        [Parameter]
        [ValidateRange(0.0, 1.0)]
        public Nullable<float> Alpha { get; set; }

        [Parameter]
        [ValidateRange(0.0, float.MaxValue)]
        public Nullable<float> FinalTemperature { get; set; }
        
        [Parameter]
        [ValidateRange(0.0, float.MaxValue)]
        public Nullable<float> InitialTemperature { get; set; }
        
        [Parameter]
        [ValidateRange(1, Int32.MaxValue)]
        public Nullable<int> IterationsPerTemperature { get; set; }

        public OptimizeSimulatedAnnealing()
        {
            this.Alpha = null;
            this.FinalTemperature = null;
            this.InitialTemperature = null;
            this.IterationsPerTemperature = null;
        }

        protected override Heuristic CreateHeuristic()
        {
            OrganonConfiguration organonConfiguration = new OrganonConfiguration(OrganonVariant.Create(this.TreeModel));
            SimulatedAnnealing annealer = new SimulatedAnnealing(this.Stand, organonConfiguration, this.HarvestPeriods, this.PlanningPeriods);
            if (this.Alpha.HasValue)
            {
                annealer.Alpha = this.Alpha.Value;
            }
            if (this.FinalTemperature.HasValue)
            {
                annealer.FinalTemperature = this.FinalTemperature.Value;
            }
            if (this.InitialTemperature.HasValue)
            {
                annealer.InitialTemperature = this.InitialTemperature.Value;
            }
            if (this.IterationsPerTemperature.HasValue)
            {
                annealer.IterationsPerTemperature = this.IterationsPerTemperature.Value;
            }
            return annealer;
        }
    }
}
