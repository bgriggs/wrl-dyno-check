using BigMission.WrlDynoCheck.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView;
using SkiaSharp;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Defaults;
using LiveChartsCore;
using System.Linq;
using MathNet.Numerics;

namespace BigMission.WrlDynoCheck.ViewModels;

public partial class DynoRunViewModel : ObservableObject
{
    public string Name { get; set; } = "Run";
    public SortedDictionary<DateTime, ChannelValue> Rpm { get; } = [];
    public SortedDictionary<DateTime, ChannelValue> Power { get; } = [];

    private static readonly SKColor s_blue = new(25, 118, 210);
    private static readonly SKColor s_red = new(229, 57, 53);
    //private static readonly SKColor s_yellow = new(198, 167, 0);

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
            DataLabelsFormatter = (point) => point.Coordinate.PrimaryValue.ToString("F2"),
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

    public ICartesianAxis[] YAxes { get; set; } =
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

    public Axis[] XAxes { get; set; } =
    [
        new Axis
        {
            Name = "RPM",
            ShowSeparatorLines = true,
            SeparatorsPaint = new SolidColorPaint(new SKColor(220, 220, 220))
        }
    ];

    [ObservableProperty]
    private string penalty = string.Empty;
    [ObservableProperty]
    private string peakPower = string.Empty;
    [ObservableProperty]
    private string lowerRange = string.Empty;
    [ObservableProperty]
    private string upperRange = string.Empty;


    public DynoRunViewModel()
    {
        Name = $"Run {DateTime.Now:MM-dd-yyyy hh:mm:ss}";
    }


    public void Process()
    {
        // Interpolate the RPM data to match the power data timestamp
        //var interpolation = Interpolate.Linear(Rpm.Values.Select(r => (double)r.Value), Rpm.Keys.Select(t => t.ToOADate()));
        var hpSeries = new List<ObservablePoint>();

        foreach (var hp in Power)
        {
            var rpm = Rpm.MinBy(r => Math.Abs((r.Key - hp.Key).TotalMilliseconds));
            //var rpm = interpolation.Interpolate(hp.Key.ToOADate());
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
        Series.Add(new LineSeries<ObservablePoint>
        {
            Name = "Peak Power",
            ScalesYAt = 0,
            //GeometrySize = 0,
            //GeometryFill = null,
            //GeometryStroke = null,
            //LineSmoothness = 0,
            Fill = null,
            //Stroke = new SolidColorPaint(s_blue) { StrokeThickness = 2 },
            DataLabelsFormatter = (point) => point.Coordinate.PrimaryValue.ToString("F2"),
            Values = [new(peakRpm.Value.Value, peakHp)]
        });

        // Traverse the power values to find the lower HP value in the sample set
        ChannelValue? lowerHp = null;
        var lowerHpCalc = peakHp - (peakHp * 0.03);
        for (int i = peakIdx; i >= 0; i--)
        {
            if (powerValues[i].Value <= lowerHpCalc)
            {
                lowerHp = powerValues[i];
                break;
            }
        }

        // Determine the RPM range for the lower HP value
        if (lowerHp != null)
        {
            var lowerRpm = Rpm.MinBy(r => Math.Abs((r.Key - lowerHp.Time).TotalMilliseconds));
            LowerRange = $"{lowerHp.Value:0.##} - {peakHp:0.##}";

            // Determine the RPM range for the lower HP value
            var perc3Rpm = peakRpm.Value.Value - lowerRpm.Value.Value;
            if (perc3Rpm <= 0.9999)
            {
                // add green series
                var greenSeries = new List<ObservablePoint>();
                for (int i = peakIdx; i >= 0; i--)
                {
                    if (powerValues[i].Value > lowerHpCalc)
                    {
                        var rpm = Rpm.MinBy(r => Math.Abs((r.Key - powerValues[i].Time).TotalMilliseconds));
                        greenSeries.Add(new(rpm.Value.Value, powerValues[i].Value));
                        break;
                    }
                }

                Series.Insert(0, new LineSeries<ObservablePoint>
                {
                    Name = "No Penalty",
                    ScalesYAt = 0,
                    GeometrySize = 0,
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 0,
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColors.LightGreen) { StrokeThickness = 4 },
                    DataLabelsFormatter = (point) => point.Coordinate.PrimaryValue.ToString("F2"),
                    Values = greenSeries
                });
            }
            else if (perc3Rpm <= 1249.9)
            {
                // add green series
                // add yellow series
            }
            else if (perc3Rpm > 1249.9)
            {
                // add green series
                // add yellow series
                // add red series
            }
        }
        else
        {
            LowerRange = "N/A";
        }

        // Traverse the power values to find the upper HP value in the sample set
        ChannelValue? upperHp = null;
        var upperHpCalc = peakHp + (peakHp * 0.03);
        for (int i = peakIdx; i < powerValues.Count; i++)
        {
            if (powerValues[i].Value >= upperHpCalc)
            {
                upperHp = powerValues[i];
                break;
            }
        }

        if (upperHp != null)
        {
            var upperRpm = Rpm.MinBy(r => Math.Abs((r.Key - upperHp.Time).TotalMilliseconds));
            UpperRange = $"{peakHp:0.##} - {upperHp.Value:0.##}";
        }
        else
        {
            UpperRange = "N/A";
        }
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
}
