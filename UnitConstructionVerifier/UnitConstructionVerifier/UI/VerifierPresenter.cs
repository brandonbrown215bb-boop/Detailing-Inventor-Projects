using System;
using System.IO;
using Newtonsoft.Json;

namespace UnitConstructionVerifier.UI
{
    /// <summary>
    /// Shows one verifier at a time, docked inside Inventor or popped out in its own window, and moves the same view
    /// between the two so nothing staged or edited is lost. Remembers the last choice for the next unit.
    /// </summary>
    internal sealed class VerifierPresenter : IDisposable
    {
        private readonly VerifierDockHost _dock;
        private VerifierWindow? _window;

        public VerifierPresenter(Inventor.Application app) => _dock = new VerifierDockHost(app);

        public VerifierView? Current { get; private set; }

        public bool IsDocked => Current != null && _window == null;

        public void Show(VerifierView view)
        {
            Current = view;
            view.DockToggleRequested += () => SetDocked(!IsDocked);
            Place(view, Settings.Load().Docked);
        }

        public void BringForward()
        {
            if (Current == null) return;
            if (_window != null) _window.Activate();
            else _dock.Show(Current);
        }

        /// <summary>Closes the current verifier after asking about unsaved work. False if the user cancelled.</summary>
        public bool Close()
        {
            if (Current is not VerifierView view) return true;
            if (!view.ConfirmClose()) return false;

            if (_window != null)
            {
                VerifierWindow window = _window;
                _window = null;
                window.Detach();
            }
            else
            {
                _dock.Detach();
            }

            view.Shutdown();
            Current = null;
            return true;
        }

        private void SetDocked(bool docked)
        {
            if (Current is not VerifierView view || docked == IsDocked) return;

            if (docked)
            {
                VerifierWindow? window = _window;
                _window = null;
                window?.Detach();
            }
            else
            {
                _dock.Detach();
            }

            Place(view, docked);
            Settings.Save(new Settings { Docked = IsDocked });
        }

        private void Place(VerifierView view, bool docked)
        {
            if (docked)
            {
                try
                {
                    _dock.Show(view);
                    view.SetHostMode(docked: true);
                    return;
                }
                catch (Exception ex)
                {
                    // A dock that cannot be created must not cost the user the verifier: pop it out instead.
                    DebugLogger.Log(ex, "VerifierPresenter: docking failed, showing the window instead");
                    _dock.Detach();
                }
            }

            var window = new VerifierWindow(view);
            window.Closed += (_, __) =>
            {
                if (ReferenceEquals(_window, window)) _window = null;
                if (!window.IsDetached && ReferenceEquals(Current, view)) Current = null;
            };
            _window = window;
            view.SetHostMode(docked: false);
            window.Show();
        }

        public void Dispose() => _dock.Dispose();

        /// <summary>Per-user choice of docked or popped out, in %APPDATA%\UnitConstructionVerifier\settings.json.</summary>
        internal sealed class Settings
        {
            private static readonly string Path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UnitConstructionVerifier", "settings.json");

            public bool Docked { get; set; }

            public static Settings Load()
            {
                try
                {
                    if (File.Exists(Path)) return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(Path)) ?? new Settings();
                }
                catch (Exception ex)
                {
                    DebugLogger.Log(ex, "VerifierPresenter.Settings.Load");
                }

                return new Settings();
            }

            public static void Save(Settings settings)
            {
                try
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                    File.WriteAllText(Path, JsonConvert.SerializeObject(settings, Formatting.Indented));
                }
                catch (Exception ex)
                {
                    DebugLogger.Log(ex, "VerifierPresenter.Settings.Save");
                }
            }
        }
    }
}
