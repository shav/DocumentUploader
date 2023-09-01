using System;
using System.CommandLine;
using System.Threading.Tasks;

namespace Sungero.RxCmd
{
  /// <summary>
  /// Команда управления документами.
  /// </summary>
  public class DocumentsCommand : BaseCommand
  {
    public DocumentsCommand(string name, string description)
        : base(name, description)
    {
      // Импортировать документы.
      this.Add(new BaseCommand("import", "Import documents into Directum RX from a specified location.")
               {
                 new Option<string>("--path", "Full path to the folder with documents to import."),
                 new Option<TimeSpan>("--agentStartTimeout", () => TimeSpan.Zero, "Timeout for starting all agents (by default 0)."),
                 new Option<int>("--portionSize", () => -1, "Size of uploading document portion (by default all documents in subfolder)."),
                 new Option<TimeSpan>("--uploadPortionsInterval", () => TimeSpan.Zero, "Time interval between uploading document portions (by default 0)."),
                 new Option<TimeSpan>("--uploadInterval", () => TimeSpan.Zero, "Time interval between uploading documents in portion for sequential mode (by default 0)."),
                 new Option<DocumentUploadOrder>("--uploadOrder", () => DocumentUploadOrder.Sequential, "Order of document uploading in portion: Sequential or Parallel (by default Sequential)."),
                 new Option<int>("--batchSize", () => 1, "Document batch size for batch-mode (should be no more than portion size). If should not use batch mode set zero value (by default 1)."),
                 new Option<bool>("--trace", () => true, "Enable documents importing trace."),
               }.WithHandler(typeof(DocumentsCommand), nameof(ImportDocumentsHandler)));
    }

    /// <summary>
    /// Обработчик команды импорта документов.
    /// </summary>
    /// <param name="username">Имя пользователя.</param>
    /// <param name="password">Пароль.</param>
    /// <param name="service">Адрес сервиса интеграции.</param>
    /// <param name="path">Папка, откуда импортировать.</param>
    /// <param name="agentStartTimeout">Время, за которое должны стартовать все агенты.</param>
    /// <param name="uploadOrder">Порядок загрузки документов в порции.</param>
    /// <param name="portionSize">Размер порции загружаемых документов.</param>
    /// <param name="uploadPortionsInterval">Интервал времени между отправками порций документов в одном агенте.</param>
    /// <param name="uploadInterval">Интервал времени между отправкой документов внутри порций (только в последовательном режиме).</param>
    /// <param name="batchSize">Размер пакета документов для загрузки в пакетном режиме.</param>
    /// <param name="trace">Нужно ли логировать процесс загрузки документов?</param>
    /// <returns>Код возврата.</returns>
    public static async Task<int> ImportDocumentsHandler(string username, string password, string service, string path,
      TimeSpan agentStartTimeout, DocumentUploadOrder uploadOrder,
      int portionSize, TimeSpan uploadPortionsInterval, TimeSpan uploadInterval, int batchSize, bool trace)
    {
      if (portionSize > 0 && batchSize>=0 && batchSize > portionSize)
      {
        throw new ArgumentException($"Batch size must be no more than portion size", nameof(batchSize));
      }

      var uploadSettings = new DocumentUploadSettings
      {
        AgentStartTimeout = agentStartTimeout,
        UploadOrder = uploadOrder,
        PortionSize = portionSize,
        BatchSize = batchSize,
        UploadPortionsInterval = uploadPortionsInterval,
        UploadInterval = uploadInterval,
        IsTraceEnabled = trace
      };

      var clientSettings = new IntegrationServiceSettings
      {
        UserName = username,
        Password = password,
        ServiceUrl = service,
        // TODO: Для дискретного режима загрузки нужно загружать результат только для запроса создания документа без версий.
        // Для запроса заполнения тела результат при этот не нужен.
        NeedReturnResult = batchSize <= 0
      };

      return await new CashReportImporter(clientSettings).ImportFrom(path, uploadSettings).ConfigureAwait(false);
    }
  }
}
