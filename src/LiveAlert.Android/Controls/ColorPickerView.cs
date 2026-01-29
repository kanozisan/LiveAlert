using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace LiveAlert;

public sealed class ColorPickerView : ContentView
{
    public static readonly BindableProperty SelectedColorProperty = BindableProperty.Create(
        nameof(SelectedColor),
        typeof(Color),
        typeof(ColorPickerView),
        Colors.Black,
        BindingMode.TwoWay,
        propertyChanged: OnSelectedColorChanged);

    private readonly Slider _redSlider;
    private readonly Slider _greenSlider;
    private readonly Slider _blueSlider;
    private readonly BoxView _preview;
    private bool _updating;

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public ColorPickerView()
    {
        _preview = new BoxView
        {
            WidthRequest = 32,
            HeightRequest = 32,
            CornerRadius = 4,
            Color = SelectedColor
        };

        _redSlider = CreateSlider();
        _greenSlider = CreateSlider();
        _blueSlider = CreateSlider();

        _redSlider.ValueChanged += (_, _) => UpdateFromSliders();
        _greenSlider.ValueChanged += (_, _) => UpdateFromSliders();
        _blueSlider.ValueChanged += (_, _) => UpdateFromSliders();

        var rows = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                BuildRow("R", _redSlider),
                BuildRow("G", _greenSlider),
                BuildRow("B", _blueSlider)
            }
        };

        var grid = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            }
        };
        grid.Add(_preview);
        Grid.SetColumn(_preview, 0);
        grid.Add(rows);
        Grid.SetColumn(rows, 1);

        Content = grid;

        UpdateSliders(SelectedColor);
    }

    private static Slider CreateSlider()
    {
        return new Slider
        {
            Minimum = 0,
            Maximum = 255
        };
    }

    private static Grid BuildRow(string label, Slider slider)
    {
        var grid = new Grid
        {
            ColumnSpacing = 6,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            }
        };
        var labelView = new Label
        {
            Text = label,
            WidthRequest = 16,
            VerticalTextAlignment = TextAlignment.Center
        };
        grid.Add(labelView);
        grid.Add(slider);
        Grid.SetColumn(slider, 1);
        return grid;
    }

    private void UpdateFromSliders()
    {
        if (_updating) return;
        var color = Color.FromRgb((byte)_redSlider.Value, (byte)_greenSlider.Value, (byte)_blueSlider.Value);
        _preview.Color = color;
        SelectedColor = color;
    }

    private void UpdateSliders(Color color)
    {
        _updating = true;
        _redSlider.Value = Math.Round(color.Red * 255);
        _greenSlider.Value = Math.Round(color.Green * 255);
        _blueSlider.Value = Math.Round(color.Blue * 255);
        _preview.Color = color;
        _updating = false;
    }

    private static void OnSelectedColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ColorPickerView picker && newValue is Color color)
        {
            picker.UpdateSliders(color);
        }
    }
}
