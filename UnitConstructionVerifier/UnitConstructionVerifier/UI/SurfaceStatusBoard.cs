using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.UI
{
    /// <summary>
    /// Holds the current <see cref="UnitStatus"/> for the surface list templates. Each template binds
    /// (row, Version) through this converter, so bumping <see cref="Version"/> refreshes every dot and badge
    /// without touching the persisted surface rows.
    /// </summary>
    public sealed class SurfaceStatusBoard : INotifyPropertyChanged, IMultiValueConverter
    {
        private UnitStatus _status = UnitStatus.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Version { get; private set; }

        public UnitStatus Status => _status;

        public void Update(UnitStatus status)
        {
            _status = status;
            Version++;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Version)));
        }

        public static string? SurfaceIamOf(object? row) => row switch
        {
            RoofSurfaceRow roof => roof.SourceSurfaceIam,
            WallSurfaceRow wall => wall.SourceSurfaceIam,
            BaseSurfaceRow baseRow => baseRow.SourceSurfaceIam,
            _ => null
        };

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            object? row = values.Length > 0 ? values[0] : null;
            return _status.For(SurfaceIamOf(row));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
