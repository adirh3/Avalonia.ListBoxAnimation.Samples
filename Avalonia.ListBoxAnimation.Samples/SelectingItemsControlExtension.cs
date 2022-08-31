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

        if (selectingItemsControl.ItemContainerGenerator
                .ContainerFromIndex(newIndex) is not ContentControl newSelection ||
            selectingItemsControl.ItemContainerGenerator.ContainerFromIndex(oldIndex) is not Visual oldSelection)
            return;
        StartOffsetAnimation(newSelection, oldSelection);
    }

    private static async void StartOffsetAnimation(TemplatedControl newSelection, Visual oldSelection)
    {
        // Find the indicator border
        if (newSelection.GetTemplateChildren().FirstOrDefault(s => s.Name == "PART_SelectedPipe") is not Visual
            borderPipe)
            return;

        // Get the composition visuals for all controls
        CompositionVisual? pipeVisual = ElementComposition.GetElementVisual(borderPipe);
        CompositionVisual? newSelectionVisual = ElementComposition.GetElementVisual(newSelection);
        CompositionVisual? oldSelectionVisual = ElementComposition.GetElementVisual(oldSelection);
        if (pipeVisual == null || newSelectionVisual == null || oldSelectionVisual == null) return;

        // Calculate the offset between old and new selections
        Vector3 selectionOffset = oldSelectionVisual.Offset - newSelectionVisual.Offset;
        // Check whether the offset is vertical (e.g. ListBox) or horizontal (e.g. TabControl)
        // Note this code assumes the items are aligned in the SelectingItemsControl
        bool isVerticalOffset = selectionOffset.Y != 0;
        float offset = isVerticalOffset ? selectionOffset.Y : selectionOffset.X;

        Compositor compositor = pipeVisual.Compositor;
        // This is required
        await compositor.RequestCommitAsync();
        var quadraticEaseIn = new QuadraticEaseIn();

        // Create new offset animation between old selection position to the current position
        Vector3KeyFrameAnimation offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Target = "Offset";
        offsetAnimation.InsertKeyFrame(0f,
            isVerticalOffset ? pipeVisual.Offset with {Y = offset} : pipeVisual.Offset with {X = offset},
            quadraticEaseIn);
        offsetAnimation.InsertKeyFrame(1f, pipeVisual.Offset, quadraticEaseIn);
        offsetAnimation.Duration = TimeSpan.FromMilliseconds(250);

        // Create small scale animation so the pipe will "stretch" while it's moving
        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.Target = "Scale";
        scaleAnimation.InsertKeyFrame(0f, Vector3.One, quadraticEaseIn);
        scaleAnimation.InsertKeyFrame(0.5f,
            new Vector3(1f + (!isVerticalOffset ? 0.75f : 0f), 1f + (isVerticalOffset ? 0.75f : 0f), 1f),
            quadraticEaseIn);
        scaleAnimation.InsertKeyFrame(1f, Vector3.One, quadraticEaseIn);
        scaleAnimation.Duration = TimeSpan.FromMilliseconds(250);

        CompositionAnimationGroup compositionAnimationGroup = compositor.CreateAnimationGroup();
        compositionAnimationGroup.Add(offsetAnimation);
        compositionAnimationGroup.Add(scaleAnimation);
        pipeVisual.StartAnimationGroup(compositionAnimationGroup);
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