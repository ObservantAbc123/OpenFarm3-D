using DatabaseAccess.Helpers;
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess;

/// <summary>
/// Provides helper methods for accessing and modifying entities in
/// the OpenFarmContext database. This class coordinates access to
/// individual entity helper classes.
/// </summary>
public sealed class DatabaseAccessHelper : IDisposable
{
    private readonly OpenFarmContext _context;

    // Individual helper classes
    private readonly Lazy<ColorHelper> _colorHelper;
    private readonly Lazy<MaterialTypeHelper> _materialTypeHelper;
    private readonly Lazy<ThreadHelper> _threadHelper;
    private readonly Lazy<MessageHelper> _messageHelper;
    private readonly Lazy<EmailHelper> _emailHelper;
    private readonly Lazy<MaterialHelper> _materialHelper;
    private readonly Lazy<MaterialPricePeriodHelper> _materialPricePeriodHelper;
    private readonly Lazy<PrinterHelper> _printerHelper;
    private readonly Lazy<PrinterModelHelper> _printerModelHelper;
    private readonly Lazy<PrinterModelPricePeriodHelper> _printerModelPricePeriodHelper;
    private readonly Lazy<PrintersLoadedMaterialHelper> _printersLoadedMaterialHelper;
    private readonly Lazy<PrintHelper> _printHelper;
    private readonly Lazy<PrintJobHelper> _printJobHelper;
    private readonly Lazy<ThumbnailHelper> _thumbnailHelper;
    private readonly Lazy<UserHelper> _userHelper;
    private readonly Lazy<EmailAutoReplyRuleHelper> _emailAutoReplyRuleHelper;
    private readonly Lazy<MaintenanceHelper> _maintenanceHelper;
    private readonly Lazy<AiResponseHelper> _aiResponseHelper;


    // Disposal flag
    private bool _disposed;

    /// <summary>
    /// Gets the helper for Color-related database operations.
    /// </summary>
    public ColorHelper Colors => _colorHelper.Value;

    /// <summary>
    /// Gets the helper for MaterialType-related database operations.
    /// </summary>
    public MaterialTypeHelper MaterialTypes => _materialTypeHelper.Value;

    /// <summary>
    /// Gets the helper for Email-related database operations.
    /// </summary>
    public EmailHelper Emails => _emailHelper.Value;

    /// <summary>
    /// Gets the helper for Thread-related database operations.
    /// </summary>
    public ThreadHelper Thread => _threadHelper.Value;

    /// <summary>
    /// Gets the helper for Message-related database operations.
    /// </summary>
    public MessageHelper Message => _messageHelper.Value;

    /// <summary>
    /// Gets the helper for Material-related database operations.
    /// </summary>
    public MaterialHelper Materials => _materialHelper.Value;

    /// <summary>
    /// Gets the helper for MaterialPricePeriod-related database operations.
    /// </summary>
    public MaterialPricePeriodHelper MaterialPricePeriods => _materialPricePeriodHelper.Value;

    /// <summary>
    /// Gets the helper for PrintJob-related database operations.
    /// </summary>
    public PrintJobHelper PrintJobs => _printJobHelper.Value;

    /// <summary>
    /// Gets the helper for Printer-related database operations.
    /// </summary>
    public PrinterHelper Printers => _printerHelper.Value;

    public PrintHelper Prints => _printHelper.Value;

    /// <summary>
    /// Gets the helper for User-related database operations.
    /// </summary>
    public UserHelper Users => _userHelper.Value;

    /// <summary>
    /// Gets the helper for PrinterModel-related database operations.
    /// </summary>
    public PrinterModelHelper PrinterModels => _printerModelHelper.Value;

    /// <summary>
    /// Gets the helper for PrinterModelPricePeriod-related database operations.
    /// </summary>
    public PrinterModelPricePeriodHelper PrinterModelPricePeriods => _printerModelPricePeriodHelper.Value;

    /// <summary>
    /// Gets the helper for PrintersLoadedMaterial-related database operations.
    /// </summary>
    public PrintersLoadedMaterialHelper PrintersLoadedMaterials => _printersLoadedMaterialHelper.Value;

    /// <summary>
    /// Gets the helper for Thumbnail-related database operations.
    /// </summary>
    public ThumbnailHelper Thumbnail => _thumbnailHelper.Value;

    public AiResponseHelper AiResponses => _aiResponseHelper.Value;

    /// <summary>
    /// Gets the helper for Maintenance-Tracking related database operations.
    /// </summary>
    public MaintenanceHelper Maintenance => _maintenanceHelper.Value;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseAccessHelper"/>
    /// class using the specified PostgreSQL connection string.
    /// </summary>
    /// <param name="connectionString">
    ///     The connection string for the OpenFarmContext database.
    /// </param>
    public DatabaseAccessHelper(string connectionString)
    {
        // Initialize the database context
        _context = new OpenFarmContext(
            new DbContextOptionsBuilder<OpenFarmContext>()
                .UseNpgsql(connectionString, o => o.UseVector())
                .EnableSensitiveDataLogging()
                .Options
        );

        // Initialize helper classes lazily
        _colorHelper = new Lazy<ColorHelper>(() => new ColorHelper(_context));
        _materialTypeHelper = new Lazy<MaterialTypeHelper>(() => new MaterialTypeHelper(_context));
        _emailHelper = new Lazy<EmailHelper>(() => new EmailHelper(_context));
        _threadHelper = new Lazy<ThreadHelper>(() => new ThreadHelper(_context));
        _messageHelper = new Lazy<MessageHelper>(() => new MessageHelper(_context));
        _materialHelper = new Lazy<MaterialHelper>(() => new MaterialHelper(_context));
        _materialPricePeriodHelper = new Lazy<MaterialPricePeriodHelper>(() => new MaterialPricePeriodHelper(_context));
        _printJobHelper = new Lazy<PrintJobHelper>(() => new PrintJobHelper(_context));
        _printerHelper = new Lazy<PrinterHelper>(() => new PrinterHelper(_context));
        _printHelper = new Lazy<PrintHelper>(() => new PrintHelper(_context));
        _userHelper = new Lazy<UserHelper>(() => new UserHelper(_context));
        _printerModelHelper = new Lazy<PrinterModelHelper>(() => new PrinterModelHelper(_context));
        _printerModelPricePeriodHelper =
            new Lazy<PrinterModelPricePeriodHelper>(() => new PrinterModelPricePeriodHelper(_context));
        _printersLoadedMaterialHelper =
            new Lazy<PrintersLoadedMaterialHelper>(() => new PrintersLoadedMaterialHelper(_context));
        _thumbnailHelper = new Lazy<ThumbnailHelper>(() => new ThumbnailHelper(_context));
        _maintenanceHelper = new Lazy<MaintenanceHelper>(() => new MaintenanceHelper(_context));
        _emailAutoReplyRuleHelper =
            new Lazy<EmailAutoReplyRuleHelper>(() => new EmailAutoReplyRuleHelper(_context));
        _aiResponseHelper = new Lazy<AiResponseHelper>(() => new AiResponseHelper(_context));
    }

    public EmailAutoReplyRuleHelper EmailAutoReplyRules => _emailAutoReplyRuleHelper.Value;

    /// <summary>
    ///     Releases all resources used by the DatabaseAccessHelperRefactored.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Releases the unmanaged resources used by the DatabaseAccessHelperRefactored and optionally releases the managed
    ///     resources.
    /// </summary>
    /// <param name="disposing">
    ///     true to release both managed and unmanaged resources; false to release only unmanaged
    ///     resources.
    /// </param>
    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing) _context.Dispose();
        _disposed = true;
    }
}
