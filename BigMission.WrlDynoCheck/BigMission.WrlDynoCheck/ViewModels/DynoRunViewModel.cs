using Avalonia.Controls;
using BigMission.WrlDynoCheck.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BigMission.WrlDynoCheck.ViewModels;

public partial class DynoRunViewModel : ObservableObject
{
    public string Name { get; set; } = "Run";
    public SortedDictionary<DateTime, ChannelValue> Rpm { get; } = [];
    public SortedDictionary<DateTime, ChannelValue> Power { get; } = [];
    private const double FLAT_PERC = 0.03;

    private static readonly SKColor s_blue = new(25, 118, 210);

    public List<ISeries> Series { get; } =
    [
        new LineSeries<ObservablePoint>
        {
            Name = "Power",
            ScalesYAt = 0,
            GeometrySize = 0,
            GeometryFill = null,
            GeometryStroke = null,
            LineSmoothness = 0,
            Fill = null,
            Stroke = new SolidColorPaint(s_blue) { StrokeThickness = 2 },
            YToolTipLabelFormatter = (point) => $"{point.Coordinate.PrimaryValue:0.#} @ {point.Coordinate.SecondaryValue:0.####}",
        },
        //new LineSeries<ObservablePoint>
        //{
        //    Name = "Torque",
        //    ScalesYAt = 1,
        //    GeometrySize = 5,
        //    LineSmoothness = 0,
        //    Fill = null,
        //},
    ];

    public ICartesianAxis[] YAxes { get; } =
    [
        new Axis
        {
            Name = "Power",
            TicksPaint = new SolidColorPaint(s_blue),
            SubticksPaint = new SolidColorPaint(s_blue),
            DrawTicksPath = true,
            //ForceStepToMin = true,
        },
        //new Axis
        //{
        //    Name = "Torque",
        //    TicksPaint = new SolidColorPaint(s_red),
        //    SubticksPaint = new SolidColorPaint(s_red),
        //    DrawTicksPath = true,
        //    ForceStepToMin = true,
        //    ShowSeparatorLines = true,
        //    Position = LiveChartsCore.Measure.AxisPosition.End
        //},
    ];

    public Axis[] XAxes { get; } =
    [
        new Axis
        {
            Name = "RPM",
            ShowSeparatorLines = true,
            SeparatorsPaint = new SolidColorPaint(new SKColor(220, 220, 220))
        }
    ];

    public List<RectangularSection> Sections { get; } = [];

    [ObservableProperty]
    private string penalty = string.Empty;
    [ObservableProperty]
    private string peakPower = string.Empty;
    [ObservableProperty]
    private string lowerPower = string.Empty;
    [ObservableProperty]
    private string upperPower = string.Empty;
    [ObservableProperty]
    private string lowerFlatRpm = "N/A";
    [ObservableProperty]
    private string upperFlatRpm = "N/A";


    public DynoRunViewModel()
    {
        Name = $"Run {DateTime.Now:MM-dd-yyyy hh:mm:ss}";
    }


    public void Process()
    {
        var hpSeries = new List<ObservablePoint>();
        foreach (var hp in Power)
        {
            var rpm = Rpm.MinBy(r => Math.Abs((r.Key - hp.Key).TotalMilliseconds));
            hpSeries.Add(new(rpm.Value.Value, hp.Value.Value));
        }

        if (hpSeries.Count > 0)
        {
            Series[0].Values = hpSeries;

            int minStep = GetSeriesStep(hpSeries);
            YAxes[0].MinStep = minStep;
        }

        var peakHp = Power.Values.Max(p => p.Value);

        PeakPower = peakHp.ToString("F2");
        var powerValues = Power.Values.ToList();
        var powerChannel = Power.First(p => p.Value.Value == peakHp).Value;
        var peakIdx = powerValues.IndexOf(powerChannel);
        var peakRpm = Rpm.MinBy(r => Math.Abs((r.Key - powerChannel.Time).TotalMilliseconds));

        // Add a point to the series for the peak power value
        Series.Add(new LineSeries<ObservablePoint>
        {
            Name = "Peak Power",
            ScalesYAt = 0,
            Fill = null,
            Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 3 },
            YToolTipLabelFormatter = (point) => $"{point.Coordinate.PrimaryValue:0.#} @ {point.Coordinate.SecondaryValue:0.####}",
            Values = [new(peakRpm.Value.Value, peakHp)]
        });

        // Add line indicating the lower HP value without penalty
        var lowerNoPenaltyRpm = Rpm.MinBy(r => Math.Abs(r.Value.Value - (peakRpm.Value.Value - 0.9999)));
        var lowerNoPenaltyPower = Power.MinBy(r => Math.Abs((r.Key - lowerNoPenaltyRpm.Key).TotalMilliseconds));

        Sections.Add(new RectangularSection
        {
            Yi = lowerNoPenaltyPower.Value.Value,
            Yj = lowerNoPenaltyPower.Value.Value,
            Stroke = new SolidColorPaint
            {
                Color = SKColors.Red,
                StrokeThickness = 2,
                PathEffect = new DashEffect([6, 6])
            }
        });

        // Add rectangle indicating no penalty rpm range
        Sections.Add(new RectangularSection
        {
            Xi = peakRpm.Value.Value - 0.9999,
            Xj = peakRpm.Value.Value + 0.9999,
            Fill = new SolidColorPaint { Color = SKColors.Green.WithAlpha(40) }
        });

        int totalPenalty = 0;

        // Traverse the power values to find the lower HP value in the sample set
        var lowerHp = GetLowerPower(peakHp, peakIdx, powerValues);

        // Determine the RPM range for the lower HP value
        if (lowerHp != null)
        {
            var lowerRpm = Rpm.MinBy(r => Math.Abs((r.Key - lowerHp.Time).TotalMilliseconds));
            LowerPower = $"{lowerHp.Value:0.##}";

            double flatRange = Math.Abs(lowerRpm.Value.Value - peakRpm.Value.Value);
            LowerFlatRpm = $"{flatRange:0.####}";

            var lowerPenalty = TraverseFlatSeriesPenaltyRanges(hpSeries, lowerRpm.Value, peakRpm.Value);
            totalPenalty += lowerPenalty;
        }
        else
        {
            LowerPower = "N/A";
        }

        // Traverse the power values to find the upper HP value in the sample set
        var upperHp = GetUpperPower(peakHp, peakIdx, powerValues);
        if (upperHp != null)
        {
            var upperRpm = Rpm.MinBy(r => Math.Abs((r.Key - upperHp.Time).TotalMilliseconds));
            UpperPower = $"{upperHp.Value:0.##}";
            
            double flatRange = Math.Abs(upperRpm.Value.Value - peakRpm.Value.Value);
            UpperFlatRpm = $"{flatRange:0.####}";

            var upperPenalty = TraverseFlatSeriesPenaltyRanges(hpSeries, upperRpm.Value, peakRpm.Value);
            totalPenalty += upperPenalty;
        }
        else
        {
            UpperPower = "N/A";
        }

        Penalty = $"{totalPenalty / -10.0:0.#}";
    }

    /// <summary>
    /// Traverse the power values to find the lower 3% HP value in the sample set.
    /// </summary>
    private static ChannelValue? GetLowerPower(float peakHp, int peakIdx, List<ChannelValue> powerValues)
    {
        ChannelValue? lowerHp = null;
        var lowerHpCalc = peakHp - (peakHp * FLAT_PERC);
        for (int i = peakIdx; i >= 0; i--)
        {
            if (powerValues[i].Value <= lowerHpCalc)
            {
                lowerHp = powerValues[i];
                break;
            }
        }
        return lowerHp;
    }

    /// <summary>
    /// Traverse the power values to find the upper 3% HP value in the sample set.
    /// </summary>
    private static ChannelValue? GetUpperPower(float peakHp, int peakIdx, List<ChannelValue> powerValues)
    {
        ChannelValue? upperHp = null;
        var upperHpCalc = peakHp + (peakHp * FLAT_PERC);
        for (int i = peakIdx; i < powerValues.Count; i++)
        {
            if (powerValues[i].Value >= upperHpCalc)
            {
                upperHp = powerValues[i];
                break;
            }
        }

        return upperHp;
    }

    private int TraverseFlatSeriesPenaltyRanges(List<ObservablePoint> points, ChannelValue rpmFlatPt, ChannelValue peakRpmPt)
    {
        bool isLower = rpmFlatPt.Time < peakRpmPt.Time;
       

        // Add the points between the flat and peak RPM values
        var seriesPoints = new List<ObservablePoint>();
        foreach (var pt in points)
        {
            if (isLower && pt.X >= rpmFlatPt.Value && pt.X <= peakRpmPt.Value)
            {
                seriesPoints.Add(pt);
            }
            // Upper end of curve
            else if (!isLower && pt.X <= rpmFlatPt.Value && pt.X >= peakRpmPt.Value)
            {
                seriesPoints.Add(pt);
            }
        }

        var pointsPeakAnchor = new List<ObservablePoint>(seriesPoints);
        if (isLower)
        {
            pointsPeakAnchor.Reverse();
        }

        int penalty = 0;

        // Range 1: 0-0.9999 rpm
        var subRange = ExtractPoints(pointsPeakAnchor, 0.9999);
        AddSeries(SKColors.LightGreen, "Flat No Penalty (0-999.9)", subRange);

        // Range 2: 1.000-1.2499
        subRange = ExtractPoints(pointsPeakAnchor, 1.2499 - 1.000);
        AddSeries(SKColors.Yellow, "Flat -0.1 (1000-1249)", subRange);
        if (subRange.Count != 0)
        {
            penalty++;
        }

        // Range 3: 1.2500-1.4999
        subRange = ExtractPoints(pointsPeakAnchor, 1.4999 - 1.2500);
        AddSeries(SKColors.Orange, "Flat -0.1 (1250-1499)", subRange);
        if (subRange.Count != 0)
        {
            penalty++;
        }

        // Range 4: 1.5000+
        subRange = ExtractPoints(pointsPeakAnchor, 1000);
        AddSeries(SKColors.Red, "Flat -0.1 per 500 (>1500)", subRange);
        if (subRange.Count != 0)
        {
            if (subRange.Count == 1)
            {
                penalty++;
            }
            else
            {
                double range = Math.Abs(subRange.First().X!.Value - subRange.Last().X!.Value);

                // Count each 500 rpm range as a penalty
                penalty += (int)Math.Ceiling(range / 0.5);
            }
        }

        return penalty;
    }

    private static List<ObservablePoint> ExtractPoints(List<ObservablePoint> pointsPeakAnchor, double rpmDuration)
    {
        if (pointsPeakAnchor.Count == 0)
            return [];

        var subRange = new List<ObservablePoint>();
        var startRpmPoint = pointsPeakAnchor[0].X;
        foreach (var pt in pointsPeakAnchor.ToArray())
        {
            var rpmDiff = Math.Abs(pt.X!.Value - startRpmPoint!.Value);
            if (rpmDiff <= rpmDuration)
            {
                subRange.Add(pt);
                pointsPeakAnchor.Remove(pt);
            }
            else
            {
                break;
            }
        }

        return subRange;
    }

    private void AddSeries(SKColor color, string name, List<ObservablePoint> points)
    {
        if (points.Count == 0)
            return;

        Series.Insert(0, new LineSeries<ObservablePoint>
        {
            Name = name,
            ScalesYAt = 0,
            GeometrySize = 0,
            GeometryFill = null,
            GeometryStroke = null,
            LineSmoothness = 0,
            Fill = null,
            Stroke = new SolidColorPaint(color) { StrokeThickness = 8 },
            YToolTipLabelFormatter = (point) => $"{point.Coordinate.PrimaryValue:0.#} @ {point.Coordinate.SecondaryValue:0.####}",
            Values = points
        });
    }

    /// <summary>
    /// Determines the interval of axis lines based on the series range.
    /// </summary>
    private static int GetSeriesStep(List<ObservablePoint> series, int steps = 4)
    {
        var max = series?.Max(x => x.Y) ?? 0;
        var min = series?.Min(x => x.Y) ?? 0;
        if (max > 0)
        {
            return GetMinStep(max, min, steps);
        }
        return 1000;
    }

    /// <summary>
    /// Determines the interval of axis lines from a range.
    /// </summary>
    private static int GetMinStep(double max, double min, int steps)
    {
        int minStep = 2000;
        double interval = (max - min) / steps;
        if (interval < 1)
            minStep = 1;
        else if (interval < 5)
            minStep = 5;
        else if (interval < 10)
            minStep = 10;
        else if (interval < 25)
            minStep = 25;
        else if (interval < 50)
            minStep = 50;
        else if (interval < 100)
            minStep = 100;
        else if (interval < 250)
            minStep = 250;
        else if (interval < 500)
            minStep = 500;
        else if (interval < 1000)
            minStep = 1000;
        else if (interval < 1500)
            minStep = 1500;
        return minStep;
    }

    /// <summary>
    /// Close this run.
    /// </summary>
    public void RemoveRun(object parent)
    {
        var c = (Control)parent;
        if (c.DataContext is MainViewModel mvm)
        {
            // For now, do not allow the last run due to the chart crashing
            if (mvm.Runs.Count > 1)
            {
                mvm.Runs.Remove(this);
                mvm.SelectedRun = mvm.Runs.First();
            }
            
        }
    }
}
