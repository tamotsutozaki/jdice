using Jdice.Domain.Campaigns;

namespace Jdice.UnitTests.Campaigns;

public class CampaignTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 7, 15, 0, 0, TimeSpan.Zero);

    private static Campaign Criar(
        string assunto = "Novidades de agosto",
        DateTimeOffset? quando = null) =>
        Campaign.Create(
            "Disparo de agosto",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            3,
            assunto,
            "Equipe JDice",
            new Dictionary<string, string> { ["mes"] = "agosto" },
            quando ?? Agora,
            "America/Sao_Paulo",
            Guid.CreateVersion7(),
            Agora);

    [Fact]
    public void Disparo_guarda_a_versao_exata_do_modelo()
    {
        var campaign = Criar();

        // Apontar para a versão, e não para o modelo, é o que permite dizer
        // meses depois qual conteúdo foi enviado.
        Assert.Equal(3, campaign.TemplateVersionNumber);
        Assert.NotEqual(Guid.Empty, campaign.TemplateVersionId);
    }

    [Fact]
    public void Disparo_nasce_agendado()
    {
        Assert.Equal(CampaignStatus.Scheduled, Criar().Status);
    }

    [Fact]
    public void Assunto_em_branco_e_recusado()
    {
        Assert.Throws<ArgumentException>(() => Criar(assunto: "   "));
    }

    [Fact]
    public void Sem_nome_o_assunto_vira_o_titulo()
    {
        var campaign = Campaign.Create(
            null!,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            1,
            "Promoção de inverno",
            null,
            null,
            Agora,
            null,
            Guid.CreateVersion7(),
            Agora);

        Assert.Equal("Promoção de inverno", campaign.Name);
    }

    [Fact]
    public void Fuso_e_guardado_por_extenso()
    {
        // Sem o fuso, "amanhã às 9h" perde o sentido quando alguém abre a
        // tela de outro lugar.
        Assert.Equal("America/Sao_Paulo", Criar().TimeZone);
    }

    [Fact]
    public void Primeiro_worker_inicia_o_disparo()
    {
        var campaign = Criar();

        Assert.True(campaign.TryStart(Agora));
        Assert.Equal(CampaignStatus.Running, campaign.Status);
        Assert.Equal(Agora, campaign.StartedAt);
    }

    [Fact]
    public void Disparo_ja_iniciado_nao_recomeca()
    {
        var campaign = Criar();
        campaign.TryStart(Agora);

        // Recomeçar reprocessaria entregas e mandaria e-mail duplicado.
        Assert.False(campaign.TryStart(Agora.AddMinutes(1)));
        Assert.Equal(Agora, campaign.StartedAt);
    }

    [Fact]
    public void Cancelamento_so_vale_antes_de_comecar()
    {
        var campaign = Criar();
        campaign.TryStart(Agora);

        // Depois que a primeira mensagem saiu, não há como trazê-la de volta;
        // dizer que "cancelou" seria mentir para quem operou.
        Assert.Throws<InvalidOperationException>(() => campaign.Cancel());
    }

    [Fact]
    public void Disparo_agendado_pode_ser_cancelado()
    {
        var campaign = Criar();

        campaign.Cancel();

        Assert.Equal(CampaignStatus.Cancelled, campaign.Status);
    }

    [Fact]
    public void Disparo_cancelado_nao_inicia()
    {
        var campaign = Criar();
        campaign.Cancel();

        Assert.False(campaign.TryStart(Agora));
    }

    [Fact]
    public void Reagendar_muda_a_hora_e_o_fuso()
    {
        var campaign = Criar();
        var novaData = Agora.AddDays(2);

        campaign.Reschedule(novaData, "America/Manaus");

        Assert.Equal(novaData, campaign.ScheduledFor);
        Assert.Equal("America/Manaus", campaign.TimeZone);
    }

    [Fact]
    public void Reagendar_disparo_em_andamento_e_recusado()
    {
        var campaign = Criar();
        campaign.TryStart(Agora);

        Assert.Throws<InvalidOperationException>(
            () => campaign.Reschedule(Agora.AddDays(1), null));
    }

    [Fact]
    public void Entregas_sao_criadas_por_destinatario()
    {
        var campaign = Criar();

        campaign.AddDelivery(Guid.CreateVersion7(), "ana@empresa.com", Agora);
        campaign.AddDelivery(Guid.CreateVersion7(), "bruno@empresa.com", Agora);

        // Uma por pessoa: é o que permite responder quem recebeu e quem não,
        // e impede que uma falha isolada derrube o disparo inteiro.
        Assert.Equal(2, campaign.Deliveries.Count);
        Assert.All(campaign.Deliveries, entrega =>
            Assert.Equal(DeliveryStatus.Pending, entrega.Status));
    }
}
