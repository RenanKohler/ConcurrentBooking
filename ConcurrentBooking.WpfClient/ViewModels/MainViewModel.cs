using ConcurrentBooking.WpfClient.Services;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Input;

namespace ConcurrentBooking.WpfClient.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private string _apiBaseUrl = "http://localhost:5000/";
    private DateTime? _selectedDate = DateTime.Today.AddDays(1);
    private AvailabilityPeriod _selectedPeriod = AvailabilityPeriod.Morning;
    private SpecialtyListItem? _selectedSpecialty;
    private UnitFilterOption? _selectedUnit;
    private ProfessionalSearchItem? _selectedProfessional;
    private AvailableSlotItem? _selectedSlot;
    private string _slotId = string.Empty;
    private string _holdId = string.Empty;
    private string _customerId = Guid.NewGuid().ToString();
    private string _idempotencyKey = Guid.NewGuid().ToString("N");
    private string _statusMessage = "Pronto para carregar os dados clínicos.";
    private string _statusBadge = "Pronto";
    private string _resultText = string.Empty;
    private string _professionalsCount = "0 profissionais";
    private string _availabilityCount = "0 horários";
    private bool _isBusy;

    public MainViewModel()
    {
        Periods = new ObservableCollection<AvailabilityPeriod>(Enum.GetValues<AvailabilityPeriod>());

        LoadDataCommand = new AsyncRelayCommand(_ => LoadReferenceDataAsync(), _ => !IsBusy);
        SearchProfessionalsCommand = new AsyncRelayCommand(_ => SearchProfessionalsAsync(), _ => !IsBusy && SelectedSpecialty is not null && SelectedDate.HasValue);
        CreateHoldCommand = new AsyncRelayCommand(_ => CreateHoldAsync(), _ => !IsBusy);
        ConfirmHoldCommand = new AsyncRelayCommand(_ => ConfirmHoldAsync(), _ => !IsBusy);
        GenerateCustomerIdCommand = new RelayCommand(_ => { CustomerId = Guid.NewGuid().ToString(); SetResult("Novo CustomerId gerado.", "Pronto"); });
        GenerateIdempotencyKeyCommand = new RelayCommand(_ => { IdempotencyKey = Guid.NewGuid().ToString("N"); SetResult("Nova chave de idempotência gerada.", "Pronto"); });
    }

    // --- Properties ---

    public string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set => SetProperty(ref _apiBaseUrl, value);
    }

    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set => SetProperty(ref _selectedDate, value);
    }

    public AvailabilityPeriod SelectedPeriod
    {
        get => _selectedPeriod;
        set => SetProperty(ref _selectedPeriod, value);
    }

    public SpecialtyListItem? SelectedSpecialty
    {
        get => _selectedSpecialty;
        set => SetProperty(ref _selectedSpecialty, value);
    }

    public UnitFilterOption? SelectedUnit
    {
        get => _selectedUnit;
        set => SetProperty(ref _selectedUnit, value);
    }

    public ProfessionalSearchItem? SelectedProfessional
    {
        get => _selectedProfessional;
        set
        {
            if (SetProperty(ref _selectedProfessional, value) && value is not null)
            {
                _ = LoadAvailabilityAsync(value);
            }
        }
    }

    public AvailableSlotItem? SelectedSlot
    {
        get => _selectedSlot;
        set
        {
            if (SetProperty(ref _selectedSlot, value) && value is not null)
            {
                SlotId = value.SlotId.ToString();
                SetResult("Slot selecionado. Você já pode criar o hold.", "Seleção");
            }
        }
    }

    public string SlotId
    {
        get => _slotId;
        set => SetProperty(ref _slotId, value);
    }

    public string HoldId
    {
        get => _holdId;
        set => SetProperty(ref _holdId, value);
    }

    public string CustomerId
    {
        get => _customerId;
        set => SetProperty(ref _customerId, value);
    }

    public string IdempotencyKey
    {
        get => _idempotencyKey;
        set => SetProperty(ref _idempotencyKey, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string StatusBadge
    {
        get => _statusBadge;
        set => SetProperty(ref _statusBadge, value);
    }

    public string ResultText
    {
        get => _resultText;
        set => SetProperty(ref _resultText, value);
    }

    public string ProfessionalsCount
    {
        get => _professionalsCount;
        set => SetProperty(ref _professionalsCount, value);
    }

    public string AvailabilityCount
    {
        get => _availabilityCount;
        set => SetProperty(ref _availabilityCount, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    // --- Collections ---

    public ObservableCollection<SpecialtyListItem> Specialties { get; } = [];
    public ObservableCollection<UnitFilterOption> Units { get; } = [];
    public ObservableCollection<AvailabilityPeriod> Periods { get; }
    public ObservableCollection<ProfessionalSearchItem> Professionals { get; } = [];
    public ObservableCollection<AvailableSlotItem> AvailableSlots { get; } = [];

    // --- Commands ---

    public ICommand LoadDataCommand { get; }
    public ICommand SearchProfessionalsCommand { get; }
    public ICommand CreateHoldCommand { get; }
    public ICommand ConfirmHoldCommand { get; }
    public ICommand GenerateCustomerIdCommand { get; }
    public ICommand GenerateIdempotencyKeyCommand { get; }

    // --- Logic ---

    private async Task ExecuteAsync(Func<BookingApiClient, CancellationToken, Task> operation)
    {
        if (!TryCreateClient(out var client))
        {
            return;
        }

        IsBusy = true;
        SetResult("Sincronizando dados com a API...", "Carregando");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            await operation(client, cts.Token);
        }
        catch (OperationCanceledException)
        {
            SetResult("A operação excedeu o tempo limite de 15 segundos.", "Timeout");
        }
        catch (HttpRequestException ex)
        {
            SetResult($"Erro de conexão com a API: {ex.Message}", "Erro");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadReferenceDataAsync()
    {
        await ExecuteAsync(async (client, ct) =>
        {
            var specialtiesResult = await client.GetSpecialtiesAsync(ct);
            if (!specialtiesResult.IsSuccess || specialtiesResult.Data is null)
            {
                SetResult($"Erro ao carregar especialidades: {specialtiesResult.Error}", "Erro");
                return;
            }

            var unitsResult = await client.GetUnitsAsync(ct);
            if (!unitsResult.IsSuccess || unitsResult.Data is null)
            {
                SetResult($"Erro ao carregar unidades: {unitsResult.Error}", "Erro");
                return;
            }

            Specialties.Clear();
            foreach (var s in specialtiesResult.Data)
            {
                Specialties.Add(s);
            }

            if (Specialties.Count > 0)
            {
                SelectedSpecialty = Specialties[0];
            }

            Units.Clear();
            Units.Add(new UnitFilterOption(null, "Todas as unidades"));
            foreach (var u in unitsResult.Data)
            {
                Units.Add(new UnitFilterOption(u.Id, u.Name));
            }

            SelectedUnit = Units[0];
            SetResult("Filtros clínicos carregados com sucesso.", "Pronto");
        });
    }

    private async Task SearchProfessionalsAsync()
    {
        if (SelectedSpecialty is null || !SelectedDate.HasValue)
        {
            SetResult("Selecione especialidade e data para buscar profissionais.", "Atenção");
            return;
        }

        await ExecuteAsync(async (client, ct) =>
        {
            var date = DateOnly.FromDateTime(SelectedDate!.Value);
            var result = await client.GetProfessionalsAsync(SelectedSpecialty.Id, date, SelectedUnit?.Id, ct);

            if (!result.IsSuccess || result.Data is null)
            {
                SetResult($"Erro ao buscar profissionais: {result.Error}", "Erro");
                return;
            }

            Professionals.Clear();
            foreach (var p in result.Data)
            {
                Professionals.Add(p);
            }

            AvailableSlots.Clear();
            SlotId = string.Empty;
            HoldId = string.Empty;
            UpdateCounts(result.Data.Count, 0);

            SetResult(result.Data.Count == 0
                ? "Nenhum profissional encontrado para os filtros selecionados."
                : $"{result.Data.Count} profissionais encontrados.",
                result.Data.Count == 0 ? "Sem dados" : "Pronto");
        });
    }

    private async Task LoadAvailabilityAsync(ProfessionalSearchItem professional)
    {
        if (!SelectedDate.HasValue)
        {
            SetResult("Selecione uma data para consultar disponibilidade.", "Atenção");
            return;
        }

        await ExecuteAsync(async (client, ct) =>
        {
            var date = DateOnly.FromDateTime(SelectedDate!.Value);
            var result = await client.GetAvailabilityAsync(professional.ProfessionalId, date, SelectedPeriod, professional.ClinicUnitId, ct);

            if (!result.IsSuccess || result.Data is null)
            {
                SetResult($"Erro ao carregar disponibilidade: {result.Error}", "Erro");
                return;
            }

            AvailableSlots.Clear();
            foreach (var slot in result.Data)
            {
                AvailableSlots.Add(slot);
            }

            AvailabilityCount = result.Data.Count == 1 ? "1 horário" : $"{result.Data.Count} horários";

            SetResult(result.Data.Count == 0
                ? "Não há horários disponíveis para este profissional e período."
                : $"{result.Data.Count} horários disponíveis carregados.",
                result.Data.Count == 0 ? "Sem dados" : "Pronto");
        });
    }

    private async Task CreateHoldAsync()
    {
        if (!TryParseGuid(SlotId, "SlotId", out var slotId) ||
            !TryParseGuid(CustomerId, "CustomerId", out var customerId) ||
            !TryParseRequired(IdempotencyKey, "IdempotencyKey", out var idempotencyKey))
        {
            return;
        }

        await ExecuteAsync(async (client, ct) =>
        {
            var result = await client.CreateHoldAsync(slotId, customerId, idempotencyKey, ct);
            if (!result.IsSuccess || result.Data is null)
            {
                SetResult($"Erro ao criar hold: {result.Error}", "Erro");
                return;
            }

            HoldId = result.Data.HoldId.ToString();
            SetResult($"Hold criado com sucesso.\nHoldId: {result.Data.HoldId}\nExpira em: {result.Data.ExpiresAt:yyyy-MM-dd HH:mm:ss}", "Hold ativo");

            if (SelectedProfessional is not null)
            {
                await LoadAvailabilityAsync(SelectedProfessional);
            }
        });
    }

    private async Task ConfirmHoldAsync()
    {
        if (!TryParseGuid(HoldId, "HoldId", out var holdId) ||
            !TryParseGuid(CustomerId, "CustomerId", out var customerId) ||
            !TryParseRequired(IdempotencyKey, "IdempotencyKey", out var idempotencyKey))
        {
            return;
        }

        await ExecuteAsync(async (client, ct) =>
        {
            var result = await client.ConfirmHoldAsync(holdId, customerId, idempotencyKey, ct);
            if (!result.IsSuccess || result.Data is null)
            {
                SetResult($"Erro ao confirmar hold: {result.Error}", "Erro");
                return;
            }

            SetResult($"Booking confirmado com sucesso.\nBookingId: {result.Data.BookingId}", "Confirmado");

            if (SelectedProfessional is not null)
            {
                await LoadAvailabilityAsync(SelectedProfessional);
            }
        });
    }

    // --- Helpers ---

    private void SetResult(string message, string badge)
    {
        ResultText = message;
        StatusMessage = message;
        StatusBadge = badge;
    }

    private void UpdateCounts(int professionals, int availability)
    {
        ProfessionalsCount = professionals == 1 ? "1 profissional" : $"{professionals} profissionais";
        AvailabilityCount = availability == 1 ? "1 horário" : $"{availability} horários";
    }

    private bool TryCreateClient(out BookingApiClient client)
    {
        client = null!;

        if (!Uri.TryCreate(ApiBaseUrl?.Trim(), UriKind.Absolute, out var baseUri))
        {
            SetResult("URL base da API inválida.", "Erro");
            return false;
        }

        var httpClient = new HttpClient
        {
            BaseAddress = baseUri,
            Timeout = Timeout.InfiniteTimeSpan
        };

        client = new BookingApiClient(httpClient);
        return true;
    }

    private bool TryParseGuid(string? value, string fieldName, out Guid parsed)
    {
        if (Guid.TryParse(value, out parsed))
        {
            return true;
        }

        SetResult($"Campo inválido: {fieldName} deve ser um GUID válido.", "Atenção");
        return false;
    }

    private bool TryParseRequired(string? value, string fieldName, out string parsed)
    {
        parsed = value?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(parsed))
        {
            return true;
        }

        SetResult($"Campo obrigatório: {fieldName}.", "Atenção");
        return false;
    }
}

public sealed record UnitFilterOption(Guid? Id, string Name);
