namespace PES6Connect.App;

internal enum Statut
{
    AVenir,
    EnCours,
    Ok,
    Attention,
    Erreur,
}

/// <summary>Une ligne de la liste de vérifications : pastille, titre, explication, bouton.</summary>
internal sealed class LigneVerification : TableLayoutPanel
{
    private readonly Label _pastille = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Emoji", 16f),
        Margin = new Padding(0, 2, 8, 0),
    };
    private readonly Label _titre = new() { AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
    private readonly Label _detail = new() { AutoSize = true, Font = new Font("Segoe UI", 9.5f), MaximumSize = new Size(420, 0) };
    private readonly Button _bouton = new() { AutoSize = true, Visible = false, Font = new Font("Segoe UI", 9.5f), Padding = new Padding(6, 2, 6, 2) };
    private Func<Task>? _action;

    public LigneVerification(string titre)
    {
        Titre = titre;
        ColumnCount = 3;
        RowCount = 1;
        AutoSize = true;
        Dock = DockStyle.Top;
        Padding = new Padding(4, 8, 4, 8);
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var textes = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        textes.Controls.Add(_titre);
        textes.Controls.Add(_detail);

        Controls.Add(_pastille, 0, 0);
        Controls.Add(textes, 1, 0);
        Controls.Add(_bouton, 2, 0);

        _titre.Text = titre;
        _bouton.Click += async (_, _) => await ExecuterActionAsync();
        Definir(Statut.AVenir, "");
    }

    public string Titre { get; }
    public Statut Statut { get; private set; }
    public string Detail => _detail.Text;

    public void Definir(Statut statut, string detail)
    {
        Statut = statut;
        _pastille.Text = statut switch
        {
            Statut.Ok => "✅",
            Statut.Attention => "⚠️",
            Statut.Erreur => "❌",
            Statut.EnCours => "⏳",
            _ => "⚪",
        };
        _detail.Text = detail;
        _titre.ForeColor = statut == Statut.AVenir ? SystemColors.GrayText : SystemColors.ControlText;
        _detail.ForeColor = statut == Statut.AVenir ? SystemColors.GrayText : SystemColors.ControlText;
    }

    /// <summary>Affiche un bouton (ou le cache si <paramref name="texte"/> est null).</summary>
    public void DefinirAction(string? texte, Func<Task>? action = null)
    {
        _action = action;
        _bouton.Text = texte ?? "";
        _bouton.Visible = texte is not null;
    }

    public void ActiverBouton(bool actif) => _bouton.Enabled = actif;

    private async Task ExecuterActionAsync()
    {
        if (_action is null)
        {
            return;
        }
        _bouton.Enabled = false;
        try
        {
            await _action();
        }
        finally
        {
            _bouton.Enabled = true;
        }
    }
}
