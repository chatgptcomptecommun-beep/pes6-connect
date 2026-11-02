using System.Drawing.Drawing2D;

namespace PES6Connect.App;

/// <summary>Pastille ronde colorée : vert ✓, orange !, rouge ✕, bleu …, gris (à venir).</summary>
internal sealed class PastilleStatut : Control
{
    private Statut _statut;

    public PastilleStatut()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                 | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(28, 28);
        Margin = new Padding(0, 2, 10, 0);
    }

    public Statut Statut
    {
        get => _statut;
        set
        {
            _statut = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var cercle = new Rectangle(1, 1, Width - 3, Height - 3);

        if (_statut == Statut.AVenir)
        {
            using var crayon = new Pen(Color.Silver, 2f);
            g.DrawEllipse(crayon, cercle);
            return;
        }

        var (couleur, symbole) = _statut switch
        {
            Statut.Ok => (Color.FromArgb(34, 139, 34), "✓"),
            Statut.Attention => (Color.FromArgb(230, 140, 0), "!"),
            Statut.Erreur => (Color.FromArgb(200, 40, 40), "✕"),
            _ => (Color.FromArgb(30, 110, 200), "…"),
        };
        using var pinceau = new SolidBrush(couleur);
        g.FillEllipse(pinceau, cercle);
        using var police = new Font("Segoe UI Symbol", Height * 0.38f, FontStyle.Bold, GraphicsUnit.Pixel);
        TextRenderer.DrawText(g, symbole, police, cercle, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
