﻿using Mars.Seem.Organon;
using Mars.Seem.Data;
using Mars.Seem.Tree;
using System;
using System.Management.Automation;
using System.Collections.Generic;

namespace Mars.Seem.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "StandFromPlot")]
    public class GetStandFromPlot : Cmdlet
    {
        [Parameter(Mandatory = true, HelpMessage = "Stand age in years, if even aged.")]
        [ValidateRange(0, 1000)]
        public int Age { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Stand area in hectares.")]
        [ValidateRange(0, 1000)]
        public float Area { get; set; }

        [Parameter(HelpMessage = "Expansion factor in units of trees per hectare.")]
        [ValidateRange(0.1F, Constant.Maximum.ExpansionFactorPerHa)]
        public float? ExpansionFactorPerHa { get; set; }

        [Parameter(HelpMessage = "Mean tethered forwarding distance in stand, in meters.")]
        [ValidateRange(0.0F, 1000.0F)]
        public float ForwardingTethered { get; set; }
        [Parameter(HelpMessage = "Mean untethered forwarding distance in stand, in meters.")]
        [ValidateRange(0.0F, 2000.0F)]
        public float ForwardingUntethered { get; set; }
        [Parameter(HelpMessage = "Mean forwarding distance on road between top of corridor and unloading point (roadside, landing, or hot load into mule train), in meters.")]
        [ValidateRange(0.0F, 2500.0F)]
        public float ForwardingRoad { get; set; }

        [Parameter]
        public TreeModel Model { get; set; }

        [Parameter(HelpMessage = "Replanting density after regeneration harvest in seedlings per hectare.")]
        [ValidateRange(1.0F, Constant.Maximum.PlantingDensityInTreesPerHectare)]
        public float? PlantingDensityPerHa { get; set; }

        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public List<int>? Plots { get; set; }

        [Parameter]
        [ValidateRange(0.0F, 200.0F)]
        public float SlopeInPercent { get; set; }

        [Parameter]
        [ValidateRange(1.0F, Constant.Maximum.SiteIndexInM)]
        public float SiteIndexInM { get; set; }

        [Parameter]
        [ValidateRange(1, Int32.MaxValue)]
        public int? Trees { get; set; }

        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string? Xlsx { get; set; }

        [Parameter]
        [ValidateNotNullOrEmpty]
        public string XlsxSheet { get; set; }

        public GetStandFromPlot()
        {
            this.Area = Constant.HarvestCost.DefaultHarvestUnitSizeInHa;
            this.ExpansionFactorPerHa = null;
            this.ForwardingTethered = Constant.HarvestCost.DefaultForwardingDistanceInStandTethered;
            this.ForwardingUntethered = Constant.HarvestCost.DefaultForwardingDistanceInStandUntethered;
            this.ForwardingRoad = Constant.HarvestCost.DefaultForwardingDistanceOnRoad;
            this.Model = TreeModel.OrganonNwo;
            this.SlopeInPercent = Constant.HarvestCost.DefaultSlopeInPercent;
            this.SiteIndexInM = 130.0F;
            this.Trees = null;
            this.Xlsx = null;
            this.XlsxSheet = "1";
        }

        protected override void ProcessRecord()
        {
            PlotsWithHeight plot;
            if (this.ExpansionFactorPerHa.HasValue)
            {
                plot = new PlotsWithHeight(this.Plots!, this.ExpansionFactorPerHa.Value);
            }
            else
            {
                plot = new PlotsWithHeight(this.Plots!);
            }
            plot.Read(this.Xlsx!, this.XlsxSheet);

            OrganonConfiguration configuration = new(OrganonVariant.Create(this.Model));
            OrganonStand stand;
            if (this.Trees.HasValue)
            {
                stand = plot.ToOrganonStand(configuration, this.Age, this.SiteIndexInM, this.Trees.Value);
            }
            else
            {
                stand = plot.ToOrganonStand(configuration, this.Age, this.SiteIndexInM);
            }

            stand.AreaInHa = this.Area;
            stand.SetCorridorLength(this.ForwardingTethered, this.ForwardingUntethered);
            stand.ForwardingDistanceOnRoad = this.ForwardingRoad;
            if (this.PlantingDensityPerHa.HasValue)
            {
                stand.PlantingDensityInTreesPerHectare = this.PlantingDensityPerHa.Value;
            }
            stand.SlopeInPercent = this.SlopeInPercent;

            this.WriteObject(stand);
        }
    }
}
