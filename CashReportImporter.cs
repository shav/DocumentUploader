using System;
using Simple.OData.Client;
using Sungero.RxCmd.Repositories;

namespace Sungero.RxCmd
{
  /// <summary>
  /// Логика импорта кассовых отчётов.
  /// </summary>
  public class CashReportImporter: DocumentsImporter
  {
    /// <summary>
    /// Типы магазинов.
    /// </summary>
    private enum StoreType { Magnit = 0, MagnitKosmetik = 1, MagnitExtra = 2 }

    /// <summary>
    /// Репозиторий городов.
    /// </summary>
    private static readonly Cities Cities = new Cities();

    /// <summary>
    /// Репозиторий работников.
    /// </summary>
    private static readonly Employees Employees = new Employees();

    /// <summary>
    /// Репозиторий регионов.
    /// </summary>
    private static readonly Regions Regions = new Regions();

    /// <summary>
    /// Генератор случайных чисел.
    /// </summary>
    private static readonly Random random = new Random();

    public CashReportImporter(IntegrationServiceSettings clientSettings)
      :base(clientSettings)
    {

    }

    protected override string DocumentTypeName => "ICashAccountingCashReports";

    protected override ODataExpression GetDocumentType(ODataExpression types)
    {
      return ((dynamic)types).ICashAccountingCashReports;
    }

    protected override dynamic GetDocumentProperties(DocumentInfo documentInfo)
    {
      var props = base.GetDocumentProperties(documentInfo);
      props.SalesCount = random.Next();
      props.EmployeeCount = random.Next();
      props.Address = Guid.NewGuid().ToString();
      props.ReportCreatedDate = DateTimeOffset.Now.AddSeconds(random.Next());
      props.OpeningDate = DateTimeOffset.Now.AddSeconds(random.Next());
      props.StoreCode = Guid.NewGuid().ToString();
      props.EfficiencyRatio = random.NextDouble();
      props.StoreType = (StoreType)random.Next(0, 3);
      props.City = new { Id = Cities.GetRandomId() };
      props.RetailOutletManager = new { Id = Employees.GetRandomId() };
      props.Supervisor = new { Id = Employees.GetRandomId() };
      props.Region = new { Id = Regions.GetRandomId() };
      props.ChiefCashier = new { Id = Employees.GetRandomId() };

      return props;
    }
  }
}
