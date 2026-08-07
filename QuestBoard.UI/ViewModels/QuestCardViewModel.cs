using System;
using System.Windows.Media;
using QuestBoard.UI.Models;

namespace QuestBoard.UI.ViewModels
{
    public class QuestCardViewModel
    {
        public QuestModel Model { get; }
        public double TiltAngle { get; }

        public QuestCardViewModel(QuestModel model)
        {
            Model = model;
            // Deterministic slight tilt based on hash of Quest ID (-2.5 deg to +2.5 deg)
            int hash = Math.Abs(model.Id.GetHashCode());
            TiltAngle = (hash % 11 - 5) * 0.5;
        }

        public string Id => Model.Id;
        public string Title => Model.Title;
        public string Status => Model.Status.ToLowerInvariant();
        public string StatusUpper => Model.Status.ToUpperInvariant();
        public string PriorityUpper => Model.Priority.ToUpperInvariant();
        public string NextAction => Model.NextAction;
        public string Owner => string.IsNullOrWhiteSpace(Model.Owner) ? "Unclaimed" : Model.Owner;
        public string Context => Model.Context;
        public string Blocker => Model.Blocker ?? string.Empty;
        public bool HasBlocker => !string.IsNullOrWhiteSpace(Model.Blocker);

        public string SealText => Status switch
        {
            "active" => "CLAIMED",
            "ready" => "READY",
            "review" => "REVIEW",
            "blocked" => "BLOCKED",
            "done" => "DONE",
            _ => StatusUpper
        };

        public SolidColorBrush SealBrush => Status switch
        {
            "active" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4AF37")), // Gold Wax
            "ready" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B8860B")),  // Dark Goldenrod
            "review" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2980B9")), // Lapis Blue
            "blocked" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0392B")),// Crimson Wax
            "done" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")),   // Forest Green
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D"))
        };

        public SolidColorBrush PriorityBrush => Model.Priority.ToLowerInvariant() switch
        {
            "high" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
            "medium" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12")),
            "low" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6"))
        };
    }
}
