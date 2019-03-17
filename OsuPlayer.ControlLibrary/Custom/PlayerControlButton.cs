﻿using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Milky.OsuPlayer.ControlLibrary.Custom
{
    public class PlayerControlButton : Button, INotifyPropertyChanged
    {
        //public new static readonly DependencyProperty BorderThicknessProperty =
        //    DependencyProperty.Register("BorderThickness", typeof(Thickness), typeof(PlayerControlButton),
        //        new PropertyMetadata(new Thickness(2, 2, 2, 2), null));
        //public new static readonly DependencyProperty BorderBrushProperty =
        //    DependencyProperty.Register("BorderBrush", typeof(Brush), typeof(PlayerControlButton),
        //        new PropertyMetadata(new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)), null));

        public Color ShadowColor
        {
            get => (Color)GetValue(ShadowColorProperty);
            set => SetValue(ShadowColorProperty, value);
        }

        public static readonly DependencyProperty ShadowColorProperty =
            DependencyProperty.Register("ShadowColor", typeof(Color), typeof(PlayerControlButton),
                new PropertyMetadata(Color.FromArgb(255, 205, 30, 93), null)); //CD1E5D

        public double ShadowOpacity
        {
            get => (double)GetValue(ShadowOpacityProperty);
            set => SetValue(ShadowOpacityProperty, value);
        }

        public static readonly DependencyProperty ShadowOpacityProperty =
            DependencyProperty.Register("ShadowOpacity", typeof(double), typeof(PlayerControlButton),
                new PropertyMetadata(0.2d, null));

        public double ImageWidth
        {
            get => (double)GetValue(ImageWidthProperty);
            set
            {
                SetValue(ImageWidthProperty, value);
                OnPropertyChanged();
            }
        }

        public static readonly DependencyProperty ImageWidthProperty =
            DependencyProperty.Register("ImageWidth", typeof(double), typeof(PlayerControlButton),
                new PropertyMetadata(32d, null));

        public double ImageHeight
        {
            get => (double)GetValue(ImageHeightProperty);
            set
            {
                SetValue(ImageHeightProperty, value);
                OnPropertyChanged();
            }
        }

        public static readonly DependencyProperty ImageHeightProperty =
            DependencyProperty.Register("ImageHeight", typeof(double), typeof(PlayerControlButton),
                new PropertyMetadata(32d, null));

        public Thickness ImageMargin
        {
            get => (Thickness)GetValue(ImageMarginProperty);
            set
            {
                SetValue(ImageMarginProperty, value);
                OnPropertyChanged();
            }
        }

        public static readonly DependencyProperty ImageMarginProperty =
            DependencyProperty.Register("ImageMargin", typeof(Thickness), typeof(PlayerControlButton),
                new PropertyMetadata(null));


        public double BorderRadius
        {
            get => (double)GetValue(BorderRadiusProperty);
            set
            {
                SetValue(BorderRadiusProperty, value);
                OnPropertyChanged();
            }
        }

        public static readonly DependencyProperty BorderRadiusProperty =
            DependencyProperty.Register("BorderRadius", typeof(double), typeof(PlayerControlButton),
                new PropertyMetadata(null));

        static PlayerControlButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PlayerControlButton), new FrameworkPropertyMetadata(typeof(PlayerControlButton)));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
