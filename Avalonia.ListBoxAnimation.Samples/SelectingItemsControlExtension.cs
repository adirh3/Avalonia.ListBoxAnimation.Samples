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
            return;

        // Get the composition visuals for all controls
        var newIndicatorVisual = ElementComposition.GetElementVisual(nextInd);
        var oldIndicatorVisual = ElementComposition.GetElementVisual(prevInd);
        if (newIndicatorVisual == null || oldIndicatorVisual == null) return;

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
        };

        var isVertical = deltaPos is { X: 0 };

        // Make sure both indicators are visible and in their original locations
        ResetElementAnimationProperties(prevInd, 1.0f);
        ResetElementAnimationProperties(nextInd, 1.0f);

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

        // Play the animation on both the previous and next indicators
        PlayIndicatorAnimations(prevInd, isVertical,
            0,
            outgoingEndPosition,
            true);

        PlayIndicatorAnimations(nextInd, 
            isVertical,
            incomingStartPosition,
            0,
            false);
    }


    private static void PlayIndicatorAnimations(Visual? indicator, bool isVertical, float from, float to,
        bool isOutgoing)
    {
        if (indicator == null) return;
        var visual = ElementComposition.GetElementVisual(indicator);
        if (visual == null) return;
        var comp = visual.Compositor;

        var size = indicator.Bounds.Size;
        var dimension = isVertical ? size.Height : size.Width;

        var beginScale = 1.0d;
        var endScale = 1.0d;

        var singleStep = new StepEasing();

        if (isOutgoing)
        {
            // fade the outgoing indicator so it looks nice when animating over the scroll area
            var opacityAnim = comp.CreateScalarKeyFrameAnimation();
            opacityAnim.InsertKeyFrame(0.0f, 1.0f);
            opacityAnim.InsertKeyFrame(0.333f, 1.0f, singleStep);
            opacityAnim.InsertKeyFrame(1.0f, 0.0f, new SplineEasing(0.1f, 0.9f, 0.2f));
            opacityAnim.Duration = TimeSpan.FromMilliseconds(600);

            visual.StartAnimation("Opacity", opacityAnim);
            return;
        }

        Vector3 ToV3(float w, float defaultV = 1f) => new(isVertical ? defaultV : w, isVertical ? w : defaultV, 1);

        var posAnim = comp.CreateVector3KeyFrameAnimation();
        var p1 = (float)(from < to ? from : (from + (dimension * (beginScale - 1))));
        var p2 = (float)(from < to ? (to + (dimension * (endScale - 1))) : to);
        posAnim.InsertKeyFrame(0.0f, ToV3(p1, 0f));
        posAnim.InsertKeyFrame(0.333f, ToV3(p2, 0f), singleStep);
        posAnim.Duration = TimeSpan.FromMilliseconds(600);
        posAnim.Target = "Offset";

        var scaleAnim = comp.CreateVector3KeyFrameAnimation();
        var s1 = (float)beginScale;
        var s2 = (float)(Math.Abs(to - from) / dimension + (from < to ? endScale : beginScale));
        var s3 = (float)endScale;
        scaleAnim.InsertKeyFrame(0.0f, ToV3(s1));
        scaleAnim.InsertKeyFrame(0.333f, ToV3(s2), new SplineEasing(0.9f, 0.1f, 1.0f, 0.2f));
        scaleAnim.InsertKeyFrame(1.0f, ToV3(s3), new SplineEasing(0.1f, 0.9f, 0.2f));
        scaleAnim.Duration = TimeSpan.FromMilliseconds(600);
        scaleAnim.Target = "Scale";

        var centerAnim = comp.CreateVector3KeyFrameAnimation();
        var c1 = (float)(from < to ? 0.0f : dimension);
        var c2 = (float)(from < to ? dimension : 0.0f);
        centerAnim.InsertKeyFrame(0.0f, ToV3(c1, 0.5f));
        centerAnim.InsertKeyFrame(1.0f, ToV3(c2, 0.5f), singleStep);
        centerAnim.Duration = TimeSpan.FromMilliseconds(600);
        centerAnim.Target = "CenterPoint";


        //
        // // This is required
        // await compositor.RequestCommitAsync();
        // var quadraticEaseIn = new SplineEasing(0.9f, 0.1f,1.0f, 0.2f);
        //
        // // Create new offset animation between old selection position to the current position
        // var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        // offsetAnimation.Target = "Offset";
        // offsetAnimation.InsertKeyFrame(0f,
        //     isVerticalOffset
        //         ? newIndicatorVisual.Offset with { Y = offset }
        //         : newIndicatorVisual.Offset with { X = offset },
        //     quadraticEaseIn);
        // offsetAnimation.InsertKeyFrame(1f, newIndicatorVisual.Offset);
        // offsetAnimation.Duration = TimeSpan.FromMilliseconds(600);
        //
        // // Create small scale animation so the pipe will "stretch" while it's moving
        // var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        // scaleAnimation.Target = "Scale";
        // scaleAnimation.InsertKeyFrame(0f, Vector3.One);
        // scaleAnimation.InsertKeyFrame(0.5f,
        //     new Vector3(1f + (!isVerticalOffset ? 0.75f : 0f), 1f + (isVerticalOffset ? 0.75f : 0f), 1f),
        //     quadraticEaseIn);
        // scaleAnimation.InsertKeyFrame(1f, Vector3.One);
        // scaleAnimation.Duration = TimeSpan.FromMilliseconds(600);
        //

        var compositionAnimationGroup = comp.CreateAnimationGroup();
        compositionAnimationGroup.Add(posAnim);
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


    void OnAnimationComplete(ref Visual? prevIndicator, ref Visual? nextIndicator)
    {
        // var indicator = m_prevIndicator;
        ResetElementAnimationProperties(prevIndicator, 0.0f);
        // m_prevIndicator.set(nullptr);

        // indicator = m_nextIndicator.get();
        ResetElementAnimationProperties(nextIndicator, 1.0f);
        // m_nextIndicator.set(nullptr);

        prevIndicator = null;
        nextIndicator = null;
    }

    static void ResetElementAnimationProperties(Visual? element, float desiredOpacity)
    {
        if (element is null) return;
        element.Opacity = desiredOpacity;
        var visual = ElementComposition.GetElementVisual(element);
        if (visual is null) return;
        visual.Offset = new Vector3(0.0f, 0.0f, 0.0f);
        visual.Scale = new Vector3(1.0f, 1.0f, 1.0f);
        visual.Opacity = desiredOpacity;
    }
}