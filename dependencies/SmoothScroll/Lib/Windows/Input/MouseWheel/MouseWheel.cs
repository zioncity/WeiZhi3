﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Logitech.Windows.MotionFlow;

namespace Logitech.Windows.Input
{
  #region MouseWheelResolution
  public enum MouseWheelResolution
  {
    Standard,
    High,
  }
  #endregion

  #region MouseWheel - Motion
  public partial class MouseWheel : NativeMotionSource
  {
    #region Constants
    public const int NativeNotchFrequencyDefault = 120;

    private static readonly DependencyPropertyKey ResolutionPropertyKey = DependencyProperty.RegisterReadOnly("Resolution", typeof(double), typeof(MouseWheel), new PropertyMetadata(1.0));
    public  static readonly DependencyProperty    ResolutionProperty    = ResolutionPropertyKey.DependencyProperty;
    #endregion

    #region Fields
    private readonly MouseDevice _mouseDevice;

    private readonly MouseWheelSingleShaftTransferCase _stdResTransferCase;
    private MouseWheelMultiShaftTransferCase _hiResTransferCase;
    private IMouseWheelTransferCase _activeTransferCase;
    private int _nativeResolutionFrequency = NativeNotchFrequencyDefault;
    #endregion

    #region Events
    public event EventHandler ActiveTransferCaseChanged;
	  #endregion

    #region Initialization
    public MouseWheel(MouseDevice mouseDevice = null, int id = 0)
    {
      Current = this;
      _mouseDevice = mouseDevice;
      Id = id;
      Name = GetName();

      _activeTransferCase = _stdResTransferCase = new MouseWheelSingleShaftTransferCase(0);
      Next = _activeTransferCase;

      if (s_newWheel != null)
        s_newWheel(this, EventArgs.Empty);
    }
    #endregion

    #region NativeMotionSource
    public override IMotionInfo PreTransmit(int timeStamp, int nativeDelta)
    {
      Current = this;
      var info = base.PreTransmit(timeStamp, nativeDelta);
      UpdateResolutionFrequency(nativeDelta);
      if (!Debugger.IsAttached)
      {
        if (Delay.TotalSeconds > 1)
          _activeTransferCase.Reset();
      }
      return info;
    }
    #endregion

    #region IMouseWheelTransferCase
    public INativeMotionTransferOutput this[double resolution] { get { return _activeTransferCase[resolution]; } }
    #endregion

    #region IMotionTransferOutput
    public IMotionInfo MotionInfo { get { return _activeTransferCase.MotionInfo; } }
    public double       Remainder { get { return _activeTransferCase.Remainder; } }

    public bool Transfer(IMotionTarget target, object context) { return _activeTransferCase.Transfer(target, context); }
    #endregion

    #region INativeMotionTransferOutput
    public int NativeRemainder { get { return _activeTransferCase.NativeRemainder; } }

    public bool Transfer(INativeMotionTarget target, object context) { return _activeTransferCase.Transfer(target, context); }
    #endregion

    #region INativeMotionConverter
    public override int NativeResolutionFrequency { get { return _nativeResolutionFrequency; } }

    public override double NativeToNormalized(int    value) { return -(double)value / NativeNotchFrequency; }
    public override int    NormalizedToNative(double value) { return (int)Math.Round(-value * NativeNotchFrequency); }
    #endregion

    #region Queries
    public MouseDevice             MouseDevice { get { return _mouseDevice; } }
    public MouseWheelResolution ResolutionMode { get { return NativeResolutionFrequency < NativeNotchFrequency ? MouseWheelResolution.High : MouseWheelResolution.Standard; } }

    public int NativeNotchFrequency
    {
      [DebuggerStepThrough]
      get { return NativeNotchFrequencyDefault; }
    }
    #endregion

    #region Properties
    public IMouseWheelTransferCase ActiveTransferCase
    {
      get { return _activeTransferCase; }
      private set
      {
        if (_activeTransferCase == value) return;
        _activeTransferCase = value;
        if (ActiveTransferCaseChanged != null)
          ActiveTransferCaseChanged(this, EventArgs.Empty);
      }
    }
    #endregion

    #region Dependency Properties
    public double Resolution
    {
      get { return (double)GetValue(ResolutionProperty); }
      private set { SetValue(ResolutionPropertyKey, value); }
    }
    #endregion

    #region Helpers
    private double GetResolution() { return (double)NativeNotchFrequency / NativeResolutionFrequency; }

    private void UpdateResolutionFrequency(int nativeDelta)
    {
      // TODO: still to revisit the algo to fix a Windows bug.
      // When a multiplier is used in the HID descriptor
      // following formula is used to compute the wheel delta:
      // wheelDelta = reportedDelta * (120 / multiplier)
      // The bug consists in Windows computing the quotient
      // before the multiplication. If the multiplier is
      // not an exact divisor of 120 the quotient is truncated.
      // For example, on a MS Explorer Mouse, the multiplier
      // is 16 which ends up to a quotient of 7 (instead of 7.5)
      // for a reported delta of 4 the wheel delta is 28 instead of 30
      //
      // For now and in this case, the computed resolution
      // is not an integral number and its value is 4.29

      nativeDelta = Math.Abs(nativeDelta);
      if (nativeDelta == _nativeResolutionFrequency) return;  // no change
      nativeDelta %= NativeNotchFrequency;
      if (nativeDelta == 0) nativeDelta = NativeNotchFrequency;
      if (nativeDelta == NativeNotchFrequency)
      {
        if (Delay.TotalSeconds <= 1) return;
      }
      else
      {
        if (nativeDelta >= _nativeResolutionFrequency &&
          (nativeDelta % _nativeResolutionFrequency) == 0 && // exact divisor
          Delay.TotalSeconds <= 1) return;
      }
      SetNativeResolutionFrequency(nativeDelta);
    }
    private void SetNativeResolutionFrequency(int value)
    {
      var previous = _nativeResolutionFrequency;
      _nativeResolutionFrequency = value;
      OnResolutionFrequencyChanged(previous, value);
    }
    private string GetName()
    {
      if (MouseDevice == Mouse.PrimaryDevice)
        return "Primary Mouse Wheel";
      else
        return string.Format("Mouse Wheel [{0}]", Id);
    }
    private void UpdateActiveTransferCase()
    {
      if (NativeResolutionFrequency >= 20)
        ActiveTransferCase = _stdResTransferCase;
      else
        ActiveTransferCase = _hiResTransferCase = new MouseWheelMultiShaftTransferCase(this);
      Next = _activeTransferCase;
    }
    private void OnResolutionFrequencyChanged(int oldResolutionFrequency, int newResolutionFrequency)
    {
      //Debug.WriteLine("OnResolutionFrequencyChanged(old = {0}, new = {1})", oldResolutionFrequency, newResolutionFrequency);
      Resolution = GetResolution();
      UpdateActiveTransferCase();
    }
    #endregion
  }
  #endregion

  #region MouseWheel - Repository
  public partial class MouseWheel
  {
    #region Fields
    internal static readonly Dictionary<MouseDevice, MouseWheel> Wheels = new Dictionary<MouseDevice, MouseWheel>();
    private static EventHandler s_newWheel;
    private static MouseWheel s_current;
    #endregion

    #region Events
    public static event EventHandler NewWheel
    {
      add
      {
        s_newWheel -= value;
        s_newWheel += value;
        foreach (var item in Wheels.Values) value(item, EventArgs.Empty);
      }
      remove { s_newWheel -= value; }
    }
    #endregion

    #region Initialization
    static MouseWheel()
    {
      Mouse.PrimaryDevice.GetWheel();
    }
    #endregion

    #region Properties
    public static MouseWheel Current
    {
      get { return s_current; }
      private set { s_current = value; }
    }
    #endregion
  }
  #endregion

  #region MouseWheel - Attached Properties
  public partial class MouseWheel
  {
    #region Enhanced

    #region Constants
    public static readonly DependencyProperty EnhancedProperty = DependencyProperty.RegisterAttached("Enhanced", typeof(bool), typeof(MouseWheel),
      new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, MouseWheelController.BeginEnsurePatchController));
    #endregion

    #region Methods
    public static bool GetEnhanced(DependencyObject obj) { return (bool)obj.GetValue(EnhancedProperty); }
    public static void SetEnhanced(DependencyObject obj, bool value) { obj.SetValue(EnhancedProperty, value); }
    #endregion

    #endregion

    #region CanContentScrollFixed
      public static readonly DependencyProperty CanContentScrollFixedProperty
          = DependencyProperty.RegisterAttached("CanContentScrollFixed", typeof(bool), typeof(MouseWheel),
      new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, MouseWheelController.BeginEnsurePatchController));
      public static bool GetCanContentScrollFixed(DependencyObject obj) { return (bool)obj.GetValue(CanContentScrollFixedProperty); }
      public static void SetCanContentScrollFixed(DependencyObject obj, bool value) { obj.SetValue(CanContentScrollFixedProperty, value); }

    #endregion

    #region Scrolling

    #region ScrollMode

    #region ScrollMode

    #region Constants
    public static readonly DependencyProperty ScrollModeProperty = DependencyProperty.RegisterAttached("ScrollMode", typeof(MouseWheelScrollMode), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelScrollMode.Auto, FrameworkPropertyMetadataOptions.Inherits, OnScrollModeChanged));
    #endregion

    #region Methods
    public static MouseWheelScrollMode GetScrollMode(DependencyObject obj) { return (MouseWheelScrollMode)obj.GetValue(ScrollModeProperty); }
    public static void                 SetScrollMode(DependencyObject obj, MouseWheelScrollMode value) {  obj.SetValue(ScrollModeProperty, value); }
    #endregion

    #region Helpers
    private static void OnScrollModeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
      SetVScrollMode(sender, (MouseWheelScrollMode)e.NewValue);
      SetHScrollMode(sender, (MouseWheelScrollMode)e.NewValue);
    }
    #endregion

    #endregion

    #region VScrollMode

    #region Constants
    public static readonly DependencyProperty VScrollModeProperty = DependencyProperty.RegisterAttached("VScrollMode", typeof(MouseWheelScrollMode), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelScrollMode.Auto, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static MouseWheelScrollMode GetVScrollMode(DependencyObject obj) { return (MouseWheelScrollMode)obj.GetValue(VScrollModeProperty); }
    public static void                 SetVScrollMode(DependencyObject obj, MouseWheelScrollMode value) {  obj.SetValue(VScrollModeProperty, value); }
    #endregion

    #endregion

    #region HScrollMode

    #region Constants
    public static readonly DependencyProperty HScrollModeProperty = DependencyProperty.RegisterAttached("HScrollMode", typeof(MouseWheelScrollMode), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelScrollMode.Auto, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static MouseWheelScrollMode GetHScrollMode(DependencyObject obj) { return (MouseWheelScrollMode)obj.GetValue(HScrollModeProperty); }
    public static void                 SetHScrollMode(DependencyObject obj, MouseWheelScrollMode value) {  obj.SetValue(HScrollModeProperty, value); }
    #endregion

    #endregion

    #endregion

    #region ScrollModifiers

    #region VScrollModifiers

    #region Constants
    public static readonly DependencyProperty VScrollModifiersProperty = DependencyProperty.RegisterAttached("VScrollModifiers", typeof(ModifierKeys), typeof(MouseWheel),
      new FrameworkPropertyMetadata(ModifierKeys.None, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static ModifierKeys GetVScrollModifiers(DependencyObject obj) { return (ModifierKeys)obj.GetValue(VScrollModifiersProperty); }
    public static void         SetVScrollModifiers(DependencyObject obj, ModifierKeys value) { obj.SetValue(VScrollModifiersProperty, value); }
    #endregion

    #endregion

    #region HScrollModifiers

    #region Constants
    public static readonly DependencyProperty HScrollModifiersProperty = DependencyProperty.RegisterAttached("HScrollModifiers", typeof(ModifierKeys), typeof(MouseWheel),
      new FrameworkPropertyMetadata(ModifierKeys.Shift, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static ModifierKeys GetHScrollModifiers(DependencyObject obj) { return (ModifierKeys)obj.GetValue(HScrollModifiersProperty); }
    public static void         SetHScrollModifiers(DependencyObject obj, ModifierKeys value) { obj.SetValue(HScrollModifiersProperty, value); }
    #endregion

    #endregion

    #endregion

    #region ScrollSmoothing
    
    #region ScrollSmoothing

    #region Constants
    public static readonly DependencyProperty ScrollSmoothingProperty = DependencyProperty.RegisterAttached("ScrollSmoothing", typeof(MouseWheelSmoothing), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelSmoothing.Smooth, FrameworkPropertyMetadataOptions.Inherits, OnScrollSmoothingChanged));
    #endregion

    #region Methods
    public static MouseWheelSmoothing GetScrollSmoothing(DependencyObject obj) { return (MouseWheelSmoothing)obj.GetValue(ScrollSmoothingProperty); }
    public static void                SetScrollSmoothing(DependencyObject obj, MouseWheelSmoothing value) {  obj.SetValue(ScrollSmoothingProperty, value); }
    #endregion

    #region Helpers
    private static void OnScrollSmoothingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
      SetVScrollSmoothing(sender, (MouseWheelSmoothing)e.NewValue);
      SetHScrollSmoothing(sender, (MouseWheelSmoothing)e.NewValue);
    }
    #endregion

    #endregion

    #region VScrollSmoothing

    #region Constants
    public static readonly DependencyProperty VScrollSmoothingProperty = DependencyProperty.RegisterAttached("VScrollSmoothing", typeof(MouseWheelSmoothing), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelSmoothing.Smooth, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static MouseWheelSmoothing GetVScrollSmoothing(DependencyObject obj) { return (MouseWheelSmoothing)obj.GetValue(VScrollSmoothingProperty); }
    public static void                SetVScrollSmoothing(DependencyObject obj, MouseWheelSmoothing value) {  obj.SetValue(VScrollSmoothingProperty, value); }
    #endregion
    
    #endregion

    #region HScrollSmoothing

    #region Constants
    public static readonly DependencyProperty HScrollSmoothingProperty = DependencyProperty.RegisterAttached("HScrollSmoothing", typeof(MouseWheelSmoothing), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelSmoothing.Smooth, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static MouseWheelSmoothing GetHScrollSmoothing(DependencyObject obj) { return (MouseWheelSmoothing)obj.GetValue(HScrollSmoothingProperty); }
    public static void                SetHScrollSmoothing(DependencyObject obj, MouseWheelSmoothing value) {  obj.SetValue(HScrollSmoothingProperty, value); }
    #endregion

    #endregion

    #endregion

    #region ScrollDebouncing
    
    #region LogicalScrollDebouncing

    #region Constants
    public static readonly DependencyProperty LogicalScrollDebouncingProperty = DependencyProperty.RegisterAttached("LogicalScrollDebouncing", typeof(MouseWheelDebouncing), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelDebouncing.None, FrameworkPropertyMetadataOptions.Inherits, OnLogicalScrollDebouncingChanged));
    #endregion

    #region Methods
    public static MouseWheelDebouncing GetLogicalScrollDebouncing(DependencyObject obj) { return (MouseWheelDebouncing)obj.GetValue(LogicalScrollDebouncingProperty); }
    public static void                 SetLogicalScrollDebouncing(DependencyObject obj, MouseWheelDebouncing value) {  obj.SetValue(LogicalScrollDebouncingProperty, value); }
    #endregion

    #region Helpers
    private static void OnLogicalScrollDebouncingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
      SetLogicalVScrollDebouncing(sender, (MouseWheelDebouncing)e.NewValue);
      SetLogicalHScrollDebouncing(sender, (MouseWheelDebouncing)e.NewValue);
    }
    #endregion

    #endregion

    #region LogicalVScrollDebouncing

    #region Constants
    public static readonly DependencyProperty LogicalVScrollDebouncingProperty = DependencyProperty.RegisterAttached("LogicalVScrollDebouncing", typeof(MouseWheelDebouncing), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelDebouncing.Auto, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static MouseWheelDebouncing GetLogicalVScrollDebouncing(DependencyObject obj) { return (MouseWheelDebouncing)obj.GetValue(LogicalVScrollDebouncingProperty); }
    public static void                 SetLogicalVScrollDebouncing(DependencyObject obj, MouseWheelDebouncing value) {  obj.SetValue(LogicalVScrollDebouncingProperty, value); }
    #endregion

    #endregion

    #region LogicalHScrollDebouncing

    #region Constants
    public static readonly DependencyProperty LogicalHScrollDebouncingProperty = DependencyProperty.RegisterAttached("LogicalHScrollDebouncing", typeof(MouseWheelDebouncing), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelDebouncing.Auto, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static MouseWheelDebouncing GetLogicalHScrollDebouncing(DependencyObject obj) { return (MouseWheelDebouncing)obj.GetValue(LogicalHScrollDebouncingProperty); }
    public static void                 SetLogicalHScrollDebouncing(DependencyObject obj, MouseWheelDebouncing value) {  obj.SetValue(LogicalHScrollDebouncingProperty, value); }
    #endregion

    #endregion

    #region PhysicalScrollDebouncing

    #region Constants
    public static readonly DependencyProperty PhysicalScrollDebouncingProperty = DependencyProperty.RegisterAttached("PhysicalScrollDebouncing", typeof(MouseWheelDebouncing), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelDebouncing.None, FrameworkPropertyMetadataOptions.Inherits, OnPhysicalScrollDebouncingChanged));
    #endregion

    #region Methods
    public static MouseWheelDebouncing GetPhysicalScrollDebouncing(DependencyObject obj) { return (MouseWheelDebouncing)obj.GetValue(PhysicalScrollDebouncingProperty); }
    public static void                   SetPhysicalScrollDebouncing(DependencyObject obj, MouseWheelDebouncing value) {  obj.SetValue(PhysicalScrollDebouncingProperty, value); }
    #endregion

    #region Helpers
    private static void OnPhysicalScrollDebouncingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
      SetPhysicalVScrollDebouncing(sender, (MouseWheelDebouncing)e.NewValue);
      SetPhysicalHScrollDebouncing(sender, (MouseWheelDebouncing)e.NewValue);
    }
    #endregion

    #endregion

    #region PhysicalVScrollDebouncing

    #region Constants
    public static readonly DependencyProperty PhysicalVScrollDebouncingProperty = DependencyProperty.RegisterAttached("PhysicalVScrollDebouncing", typeof(MouseWheelDebouncing), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelDebouncing.None, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static MouseWheelDebouncing GetPhysicalVScrollDebouncing(DependencyObject obj) { return (MouseWheelDebouncing)obj.GetValue(PhysicalVScrollDebouncingProperty); }
    public static void                 SetPhysicalVScrollDebouncing(DependencyObject obj, MouseWheelDebouncing value) {  obj.SetValue(PhysicalVScrollDebouncingProperty, value); }
    #endregion

    #endregion

    #region PhysicalHScrollDebouncing

    #region Constants
    public static readonly DependencyProperty PhysicalHScrollDebouncingProperty = DependencyProperty.RegisterAttached("PhysicalHScrollDebouncing", typeof(MouseWheelDebouncing), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelDebouncing.None, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static MouseWheelDebouncing GetPhysicalHScrollDebouncing(DependencyObject obj) { return (MouseWheelDebouncing)obj.GetValue(PhysicalHScrollDebouncingProperty); }
    public static void                 SetPhysicalHScrollDebouncing(DependencyObject obj, MouseWheelDebouncing value) {  obj.SetValue(PhysicalHScrollDebouncingProperty, value); }
    #endregion

    #endregion

    #endregion

    #region ScrollIncrement

    #region LogicalScrollIncrement

    #region Constants
    public static readonly DependencyProperty LogicalScrollIncrementProperty = DependencyProperty.RegisterAttached("LogicalScrollIncrement", typeof(MouseWheelScrollIncrement), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelScrollIncrement.StarLogicalDefault, FrameworkPropertyMetadataOptions.Inherits, OnLogicalScrollIncrementChanged));
    #endregion

    #region Methods
    public static MouseWheelScrollIncrement GetLogicalScrollIncrement(DependencyObject obj) { return (MouseWheelScrollIncrement)obj.GetValue(LogicalScrollIncrementProperty); }
    public static void                      SetLogicalScrollIncrement(DependencyObject obj, MouseWheelScrollIncrement value) {  obj.SetValue(LogicalScrollIncrementProperty, value); }
    #endregion

    #region Helpers
    private static void OnLogicalScrollIncrementChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
      SetLogicalVScrollIncrement(sender, (e.NewValue as MouseWheelScrollIncrement).Clone(Orientation.Vertical));
      SetLogicalHScrollIncrement(sender, (e.NewValue as MouseWheelScrollIncrement).Clone(Orientation.Horizontal));
    }
    #endregion

    #endregion

    #region LogicalVScrollIncrement

    #region Constants
    public static readonly DependencyProperty LogicalVScrollIncrementProperty = DependencyProperty.RegisterAttached("LogicalVScrollIncrement", typeof(MouseWheelScrollIncrement), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelScrollIncrement.StarLogicalDefaultV, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static MouseWheelScrollIncrement GetLogicalVScrollIncrement(DependencyObject obj) { return (MouseWheelScrollIncrement)obj.GetValue(LogicalVScrollIncrementProperty); }
    public static void                      SetLogicalVScrollIncrement(DependencyObject obj, MouseWheelScrollIncrement value) { obj.SetValue(LogicalVScrollIncrementProperty, value); }
    #endregion

    #endregion

    #region LogicalHScrollIncrement

    #region Constants
    public static readonly DependencyProperty LogicalHScrollIncrementProperty = DependencyProperty.RegisterAttached("LogicalHScrollIncrement", typeof(MouseWheelScrollIncrement), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelScrollIncrement.StarLogicalDefaultH, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static MouseWheelScrollIncrement GetLogicalHScrollIncrement(DependencyObject obj) { return (MouseWheelScrollIncrement)obj.GetValue(LogicalHScrollIncrementProperty); }
    public static void                      SetLogicalHScrollIncrement(DependencyObject obj, MouseWheelScrollIncrement value) { obj.SetValue(LogicalHScrollIncrementProperty, value); }
    #endregion

    #endregion

    #region PhysicalScrollIncrement

    #region Constants
    public static readonly DependencyProperty PhysicalScrollIncrementProperty = DependencyProperty.RegisterAttached("PhysicalScrollIncrement", typeof(MouseWheelScrollIncrement), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelScrollIncrement.StarPhysicalDefault, FrameworkPropertyMetadataOptions.Inherits, OnPhysicalScrollIncrementChanged));
    #endregion

    #region Methods
    public static MouseWheelScrollIncrement GetPhysicalScrollIncrement(DependencyObject obj) { return (MouseWheelScrollIncrement)obj.GetValue(PhysicalScrollIncrementProperty); }
    public static void                      SetPhysicalScrollIncrement(DependencyObject obj, MouseWheelScrollIncrement value) {  obj.SetValue(PhysicalScrollIncrementProperty, value); }
    #endregion

    #region Helpers
    private static void OnPhysicalScrollIncrementChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
      SetPhysicalVScrollIncrement(sender, (e.NewValue as MouseWheelScrollIncrement).Clone(Orientation.Vertical));
      SetPhysicalHScrollIncrement(sender, (e.NewValue as MouseWheelScrollIncrement).Clone(Orientation.Horizontal));
    }
    #endregion

    #endregion

    #region PhysicalVScrollIncrement

    #region Constants
    public static readonly DependencyProperty PhysicalVScrollIncrementProperty = DependencyProperty.RegisterAttached("PhysicalVScrollIncrement", typeof(MouseWheelScrollIncrement), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelScrollIncrement.StarPhysicalDefaultV, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static MouseWheelScrollIncrement GetPhysicalVScrollIncrement(DependencyObject obj) { return (MouseWheelScrollIncrement)obj.GetValue(PhysicalVScrollIncrementProperty); }
    public static void                      SetPhysicalVScrollIncrement(DependencyObject obj, MouseWheelScrollIncrement value) {  obj.SetValue(PhysicalVScrollIncrementProperty, value); }
    #endregion

    #endregion

    #region PhysicalHScrollIncrement

    #region Constants
    public static readonly DependencyProperty PhysicalHScrollIncrementProperty = DependencyProperty.RegisterAttached("PhysicalHScrollIncrement", typeof(MouseWheelScrollIncrement), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelScrollIncrement.StarPhysicalDefaultH, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static MouseWheelScrollIncrement GetPhysicalHScrollIncrement(DependencyObject obj) { return (MouseWheelScrollIncrement)obj.GetValue(PhysicalHScrollIncrementProperty); }
    public static void                      SetPhysicalHScrollIncrement(DependencyObject obj, MouseWheelScrollIncrement value) {  obj.SetValue(PhysicalHScrollIncrementProperty, value); }
    #endregion

    #endregion

    #endregion

    #region NestedScroll

    #region NestedScroll

    #region Constants
    public static readonly DependencyProperty NestedScrollProperty = DependencyProperty.RegisterAttached("NestedScroll", typeof(bool), typeof(MouseWheel),
      new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits, OnNestedScrollChanged));
    #endregion

    #region Methods
    public static bool GetNestedScroll(DependencyObject obj) { return (bool)obj.GetValue(NestedScrollProperty); }
    public static void SetNestedScroll(DependencyObject obj, bool value) { obj.SetValue(NestedScrollProperty, value); }
    #endregion

    #region Helpers
    private static void OnNestedScrollChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
      SetNestedVScroll(sender, (bool)e.NewValue);
      SetNestedHScroll(sender, (bool)e.NewValue);
    }
    #endregion

    #endregion

    #region NestedVScroll

    #region Constants
    public static readonly DependencyProperty NestedVScrollProperty = DependencyProperty.RegisterAttached("NestedVScroll", typeof(bool), typeof(MouseWheel),
      new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static bool GetNestedVScroll(DependencyObject obj) { return (bool)obj.GetValue(NestedVScrollProperty); }
    public static void SetNestedVScroll(DependencyObject obj, bool value) { obj.SetValue(NestedVScrollProperty, value); }
    #endregion

    #endregion

    #region NestedHScroll

    #region Constants
    public static readonly DependencyProperty NestedHScrollProperty = DependencyProperty.RegisterAttached("NestedHScroll", typeof(bool), typeof(MouseWheel),
      new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static bool GetNestedHScroll(DependencyObject obj) { return (bool)obj.GetValue(NestedHScrollProperty); }
    public static void SetNestedHScroll(DependencyObject obj, bool value) { obj.SetValue(NestedHScrollProperty, value); }
    #endregion

    #endregion

    #endregion

    #endregion

    #region Zooming

    #region ZoomModifiers

    #region Constants
    public static readonly DependencyProperty ZoomModifiersProperty = DependencyProperty.RegisterAttached("ZoomModifiers", typeof(ModifierKeys), typeof(MouseWheel),
      new FrameworkPropertyMetadata(ModifierKeys.Control, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static ModifierKeys GetZoomModifiers(DependencyObject obj) { return (ModifierKeys)obj.GetValue(ZoomModifiersProperty); }
    public static void         SetZoomModifiers(DependencyObject obj, ModifierKeys value) { obj.SetValue(ZoomModifiersProperty, value); }
    #endregion

    #endregion

    #region ZoomSmoothing

    #region Constants
    public static readonly DependencyProperty ZoomSmoothingProperty = DependencyProperty.RegisterAttached("ZoomSmoothing", typeof(MouseWheelSmoothing), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelSmoothing.Linear, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static MouseWheelSmoothing GetZoomSmoothing(DependencyObject obj) { return (MouseWheelSmoothing)obj.GetValue(ZoomSmoothingProperty); }
    public static void                SetZoomSmoothing(DependencyObject obj, MouseWheelSmoothing value) { obj.SetValue(ZoomSmoothingProperty, value); }
    #endregion

    #endregion

    #region ZoomDebouncing

    #region Constants
    public static readonly DependencyProperty ZoomDebouncingProperty = DependencyProperty.RegisterAttached("ZoomDebouncing", typeof(MouseWheelDebouncing), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelDebouncing.Auto, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static MouseWheelDebouncing GetZoomDebouncing(DependencyObject obj) { return (MouseWheelDebouncing)obj.GetValue(ZoomDebouncingProperty); }
    public static void                 SetZoomDebouncing(DependencyObject obj, MouseWheelDebouncing value) { obj.SetValue(ZoomDebouncingProperty, value); }
    #endregion

    #endregion

    #region NestedZoom

    #region Constants
    public static readonly DependencyProperty NestedZoomProperty = DependencyProperty.RegisterAttached("NestedZoom", typeof(bool), typeof(MouseWheel),
      new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits));
    #endregion

    #region Methods
    public static bool GetNestedZoom(DependencyObject obj) { return (bool)obj.GetValue(NestedZoomProperty); }
    public static void SetNestedZoom(DependencyObject obj, bool value) { obj.SetValue(NestedZoomProperty, value); }
    #endregion

    #endregion

    #endregion

    #region Adaptation

    #region Modifiers

    #region Constants
    public static readonly DependencyProperty ModifiersProperty = DependencyProperty.RegisterAttached("Modifiers", typeof(ModifierKeys), typeof(MouseWheel),
      new FrameworkPropertyMetadata(ModifierKeys.None, MouseWheelController.BeginEnsureMapController));
    #endregion

    #region Methods
    public static ModifierKeys GetModifiers(DependencyObject obj) { return (ModifierKeys)obj.GetValue(ModifiersProperty); }
    public static void         SetModifiers(DependencyObject obj, ModifierKeys value) { obj.SetValue(ModifiersProperty, value); }
    #endregion

    #endregion

    #region Smoothing

    #region Constants
    public static readonly DependencyProperty SmoothingProperty = DependencyProperty.RegisterAttached("Smoothing", typeof(MouseWheelSmoothing), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelSmoothing.None, MouseWheelController.BeginEnsureMapController));
    #endregion

    #region Methods
    public static MouseWheelSmoothing GetSmoothing(DependencyObject obj) { return (MouseWheelSmoothing)obj.GetValue(SmoothingProperty); }
    public static void                SetSmoothing(DependencyObject obj, MouseWheelSmoothing value) { obj.SetValue(SmoothingProperty, value); }
    #endregion

    #endregion

    #region Debouncing

    #region Constants
    public static readonly DependencyProperty DebouncingProperty = DependencyProperty.RegisterAttached("Debouncing", typeof(MouseWheelDebouncing), typeof(MouseWheel),
      new FrameworkPropertyMetadata(MouseWheelDebouncing.Auto, MouseWheelController.BeginEnsureMapController));
    #endregion

    #region Methods
    public static MouseWheelDebouncing GetDebouncing(DependencyObject obj) { return (MouseWheelDebouncing)obj.GetValue(DebouncingProperty); }
    public static void                 SetDebouncing(DependencyObject obj, MouseWheelDebouncing value) { obj.SetValue(DebouncingProperty, value); }
    #endregion

    #endregion

    #region NestedMotion

    #region Constants
    public static readonly DependencyProperty NestedMotionProperty = DependencyProperty.RegisterAttached("NestedMotion", typeof(bool), typeof(MouseWheel),
      new FrameworkPropertyMetadata(false, MouseWheelController.BeginEnsureMapController));
    #endregion

    #region Methods
    public static bool GetNestedMotion(DependencyObject obj) { return (bool)obj.GetValue(NestedMotionProperty); }
    public static void SetNestedMotion(DependencyObject obj, bool value) { obj.SetValue(NestedMotionProperty, value); }
    #endregion

    #endregion

    #region Increment

    #region Constants
    public static readonly DependencyProperty IncrementProperty = DependencyProperty.RegisterAttached("Increment", typeof(double), typeof(MouseWheel),
      new FrameworkPropertyMetadata(0.0, MouseWheelController.BeginEnsureMapController));
    #endregion

    #region Methods
    public static double GetIncrement(DependencyObject obj) { return (double)obj.GetValue(IncrementProperty); }
    public static void   SetIncrement(DependencyObject obj, double value) { obj.SetValue(IncrementProperty, value); }
    #endregion

    #endregion

    #region Minimum

    #region Constants
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.RegisterAttached("Minimum", typeof(double), typeof(MouseWheel),
      new FrameworkPropertyMetadata(0.0, MouseWheelController.BeginEnsureMapController));
    #endregion

    #region Methods
    public static double GetMinimum(DependencyObject obj) { return (double)obj.GetValue(MinimumProperty); }
    public static void   SetMinimum(DependencyObject obj, double value) { obj.SetValue(MinimumProperty, value); }
    #endregion

    #endregion

    #region Maximum

    #region Constants
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.RegisterAttached("Maximum", typeof(double), typeof(MouseWheel),
      new FrameworkPropertyMetadata(0.0, MouseWheelController.BeginEnsureMapController));
    #endregion

    #region Methods
    public static double GetMaximum(DependencyObject obj) { return (double)obj.GetValue(MaximumProperty); }
    public static void   SetMaximum(DependencyObject obj, double value) { obj.SetValue(MaximumProperty, value); }
    #endregion

    #endregion

    #region Value

    #region Constants
    public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached("Value", typeof(double), typeof(MouseWheel),
      new FrameworkPropertyMetadata(0.0));
    #endregion

    #region Methods
    public static double GetValue(DependencyObject obj) { return (double)obj.GetValue(ValueProperty); }
    public static void   SetValue(DependencyObject obj, double value) { obj.SetValue(ValueProperty, value); }
    #endregion

    #endregion

    #endregion

    #endregion
  }
}
