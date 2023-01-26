using System;
using System.Linq;
using System.Numerics;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.VisualTree;

namespace Avalonia.ListBoxAnimation.Samples;

public class SelectingItemsControlExtension
{
    public static readonly AttachedProperty<bool> EnableSelectionAnimationProperty =
        AvaloniaProperty.RegisterAttached<SelectingItemsControl, bool>("EnableSelectionAnimation",
            typeof(SelectingItemsControlExtension));

    static SelectingItemsControlExtension()
    {
        EnableSelectionAnimationProperty.Changed.AddClassHandler<Control>(OnEnableSelectionAnimation);
    }

    private static void OnEnableSelectionAnimation(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        if (control is SelectingItemsControl listBox)
        {
            if (args.NewValue is true)
            {
                listBox.PropertyChanged += SelectingItemsControlPropertyChanged;
            }
            else
            {
                listBox.PropertyChanged -= SelectingItemsControlPropertyChanged;
            }
        }
    }

    private static void SelectingItemsControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (sender is not SelectingItemsControl selectingItemsControl ||
            args.Property != SelectingItemsControl.SelectedIndexProperty ||
            args.OldValue is not int oldIndex || args.NewValue is not int newIndex)
            return;

        if (oldIndex == -1 || newIndex == -1)
        {
            // TODO: handle selection removal.
        }

        if (selectingItemsControl.ItemContainerGenerator
                .ContainerFromIndex(newIndex) is not ContentControl newSelection ||
            selectingItemsControl.ItemContainerGenerator
                .ContainerFromIndex(oldIndex) is not ContentControl oldSelection)
            return;

        Console.WriteLine($"{oldIndex} -> {newIndex}");

        StartOffsetAnimation(newSelection, oldSelection);
    }

    private static async void StartOffsetAnimation(TemplatedControl nextSelection, TemplatedControl prevSelection)
    {
        // Find the indicator border
        if (prevSelection.GetTemplateChildren().FirstOrDefault(s => s.Name == "PART_SelectedPipe") is not Visual
                prevInd ||
            nextSelection.GetTemplateChildren().FirstOrDefault(s => s.Name == "PART_SelectedPipe") is not Visual
                nextInd)
        {
            Console.WriteLine("Weird");
            return;
        }

        var tmpPrevPos = prevInd.GetVisualRoot()?.TransformToVisual(prevInd)?.Transform(new Point(0, 0));
        var tmpNextPos = nextInd.GetVisualRoot()?.TransformToVisual(nextInd)?.Transform(new Point(0, 0));
        var tmpDelta = tmpPrevPos - tmpNextPos;

        if (tmpDelta is not { } deltaPos ||
            deltaPos is { X: > 0, Y: > 0 } ||
            tmpNextPos is not { } nextPos ||
            tmpPrevPos is not { } prevPos)
        {
            Console.WriteLine("WTF");
            return;
        }

        ;

        var isVertical = deltaPos is { X: 0 };
        var dist = (float)deltaPos.X + (float)deltaPos.Y;

        float outgoingEndPosition, incomingStartPosition;

        if (isVertical)
        {
            outgoingEndPosition = (float)(nextPos.Y - prevPos.Y);
            incomingStartPosition = (float)(prevPos.Y - nextPos.Y);
        }
        else
        {
            outgoingEndPosition = (float)(nextPos.X - prevPos.X);
            incomingStartPosition = (float)(prevPos.X - nextPos.X);
        }

        Console.WriteLine($"f {outgoingEndPosition} {incomingStartPosition}");

        var visual = ElementComposition.GetElementVisual(nextInd);
        if (visual == null) return;
        var comp = visual.Compositor;

        await comp.RequestCommitAsync();
        var prevSize = prevInd.Bounds.Size;
        var nextSize = nextInd.Bounds.Size;

        float dir = ((isVertical ? nextPos.Y : nextPos.X) > (isVertical ? prevPos.Y : prevPos.X)) ? -1 : 1;

        // Play the animation on both the previous and next indicators
        PlayIndicatorAnimations(prevInd, isVertical, 
            0,
            0, prevSize, nextSize,
            true);


        PlayIndicatorAnimations(nextInd, isVertical,
            dir * Math.Abs(dist),
            0, prevSize, nextSize,
            false);
    }


    private static void PlayIndicatorAnimations(Visual? indicator, bool isVertical, float from, float to,
        Size beginSize, Size endSize,
        bool isOutgoing)
    {
        if (indicator == null) return;
        var visual = ElementComposition.GetElementVisual(indicator);
        if (visual == null) return;
        var comp = visual.Compositor;

        Console.WriteLine($"O: {visual.Offset}");
        Console.WriteLine($"S: {visual.Scale}");
        Console.WriteLine($"C: {visual.CenterPoint}");
        var duration = TimeSpan.FromSeconds(5);
        var size = indicator.Bounds.Size;
        var dimension = isVertical ? size.Height : size.Width;

        double beginScale, endScale;

        if (isVertical)
        {
            beginScale = beginSize.Height / size.Height;
            endScale = endSize.Height / size.Height;
        }
        else
        {
            beginScale = beginSize.Width / size.Width;
            endScale = endSize.Width / size.Width;
        }


        var singleStep = new StepEasing();
        var compositionAnimationGroup = comp.CreateAnimationGroup();

        if (isOutgoing)
        {
            // fade the outgoing indicator so it looks nice when animating over the scroll area
            var opacityAnim = comp.CreateScalarKeyFrameAnimation();
            opacityAnim.InsertKeyFrame(0.0f, 1.0f);
            opacityAnim.InsertKeyFrame(0.333f, 1.0f);
            opacityAnim.InsertKeyFrame(1.0f, 0.0f);
            opacityAnim.Duration = duration;
            opacityAnim.Target = "Opacity";
            compositionAnimationGroup.Add(opacityAnim);
        }

        Vector3 ScalarModifier(Vector3 reference, float scalar = 1f) =>
            isVertical ? reference with { Y = scalar } : reference with { X = scalar };

        var scaleAnim = comp.CreateVector3KeyFrameAnimation();
        var s1 = (float)beginScale;
        var s2 = (float)((Math.Abs(to - from) / dimension) + (from < to ? endScale : beginScale));
        var s3 = (float)endScale;

        Console.WriteLine($"s {s1} - {s2} - {s3}");

        scaleAnim.InsertKeyFrame(0.0f, ScalarModifier(visual.Scale, s1));
        scaleAnim.InsertKeyFrame(0.333f, ScalarModifier(visual.Scale, s2));
        scaleAnim.InsertKeyFrame(1.0f, ScalarModifier(visual.Scale, s3));
        scaleAnim.Duration = duration;
        scaleAnim.Target = "Scale";

        var centerAnim = comp.CreateVector3KeyFrameAnimation();
        var c1 = (float)(from < to ? 0.0f : dimension);
        var c2 = (float)(from < to ? dimension : 0.0f);

        Console.WriteLine($"c {c1} - {c2}");

        centerAnim.InsertKeyFrame(0.0f, ScalarModifier(visual.CenterPoint, c1));
        centerAnim.InsertKeyFrame(1.0f, ScalarModifier(visual.CenterPoint, c2), singleStep);
        centerAnim.Duration = duration;
        centerAnim.Target = "CenterPoint";


        compositionAnimationGroup.Add(scaleAnim);
        compositionAnimationGroup.Add(centerAnim);
        visual.StartAnimationGroup(compositionAnimationGroup);
    }


    private class StepEasing : IEasing
    {
        public double Ease(double progress)
        {
            return (Math.Abs(progress - 1) < double.Epsilon) ? 1d : 0d;
        }
    }

    public static bool GetEnableSelectionAnimation(SelectingItemsControl element)
    {
        return element.GetValue(EnableSelectionAnimationProperty);
    }

    public static void SetEnableSelectionAnimation(SelectingItemsControl element, bool value)
    {
        element.SetValue(EnableSelectionAnimationProperty, value);
    }
}