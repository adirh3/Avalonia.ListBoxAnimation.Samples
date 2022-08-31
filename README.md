# Avalonia.ListBoxAnimation.Samples
Simple item selection animation using Avalonia 11


https://user-images.githubusercontent.com/27368554/187793478-164def31-b2e9-4493-81f1-73a5b4275839.mp4




To make this kind of animation work you need 3 things -

## Indicator visual

The indicator visual needs to be in the control's template, for example for ListBoxItem
```XAML
 <ControlTemplate>
                <Panel Margin="{TemplateBinding Margin}">
                    <Border
                        Width="3.5"
                        ZIndex="1"
                        Height="{DynamicResource ListBoxItemPipeHeight}"
                        Name="PART_SelectedPipe"
                        CornerRadius="15"
                        ClipToBounds="False"
                        VerticalAlignment="Center"
                        HorizontalAlignment="Left"
                        Background="{DynamicResource SystemControlHighlightListAccentLowBrush}" />
                    <ContentPresenter Name="PART_ContentPresenter"
                                      ZIndex="0"
                                      BorderBrush="{TemplateBinding BorderBrush}"
                                      CornerRadius="{TemplateBinding CornerRadius}"
                                      ContentTemplate="{TemplateBinding ContentTemplate}"
                                      Content="{TemplateBinding Content}"
                                      FontWeight="Normal"
                                      FontSize="{DynamicResource ControlContentThemeFontSize}"
                                      Padding="{TemplateBinding Padding}"
                                      VerticalContentAlignment="{TemplateBinding VerticalContentAlignment}"
                                      HorizontalContentAlignment="{TemplateBinding HorizontalContentAlignment}" />
                </Panel>
</ControlTemplate>
```  
The "PART_SelectedPipe" `Border` is the indicator.
**Important** - you must set `ClipToBounds=False` on both the control and the indicator, otherwise the animation won't be as cool ;)

## Attached property

Create simple [AttachedProperty](https://docs.avaloniaui.net/docs/data-binding/creating-and-binding-attached-properties) for `SelectingItemsControl` -
```C#
    public static readonly AttachedProperty<bool> EnableSelectionAnimationProperty =
        AvaloniaProperty.RegisterAttached<SelectingItemsControl, bool>("EnableSelectionAnimation",
            typeof(SelectingItemControlExtension));

    static SelectingItemControlExtension()
    {
        EnableSelectionAnimationProperty.Changed.AddClassHandler<Control>(OnEnableSelectionAnimation);
    }
```
Using this we can know when an item selection change occured and get the controls of the previous and current selection.

## Animation

Using the new Composition APIs, given the two controls we can calculate the offset between them - 
```C#
// Get the composition visuals for all controls
CompositionVisual? pipeVisual = ElementComposition.GetElementVisual(borderPipe);
CompositionVisual? newSelectionVisual = ElementComposition.GetElementVisual(newSelection);
CompositionVisual? oldSelectionVisual = ElementComposition.GetElementVisual(oldSelection);
if (pipeVisual == null || newSelectionVisual == null || oldSelectionVisual == null) return;

// Calculate the offset between old and new selections
Vector3 selectionOffset = oldSelectionVisual.Offset - newSelectionVisual.Offset;
bool isVerticalOffset = selectionOffset.Y != 0;
float offset = isVerticalOffset ? selectionOffset.Y : selectionOffset.X;
```
Using that information we create a simple `Offset` animation between the old position and the new position!

```
// Create new offset animation between old selection position to the current position
Vector3KeyFrameAnimation offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
offsetAnimation.Target = "Offset";
offsetAnimation.InsertKeyFrame(0f,
    isVerticalOffset ? pipeVisual.Offset with {Y = offset} : pipeVisual.Offset with {X = offset},
    quadraticEaseIn);
offsetAnimation.InsertKeyFrame(1f, pipeVisual.Offset, quadraticEaseIn);
offsetAnimation.Duration = TimeSpan.FromMilliseconds(250);
```

