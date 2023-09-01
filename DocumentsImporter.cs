using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Simple.OData.Client;
using Sungero.Logging;
using Sungero.RxCmd.Repositories;
using IDocumentClient = Simple.OData.Client.IBoundClient<System.Collections.Generic.IDictionary<string, object>>;

namespace Sungero.RxCmd
{
  /// <summary>
  /// Логика импорта документов.
  /// </summary>
  public abstract class DocumentsImporter
  {
    /// <summary>
    /// Репозиторий приложений-редакторов.
    /// </summary>
    private static readonly AssociatedApplications AssociatedApplications = new AssociatedApplications();

    /// <summary>
    /// Логгер.
    /// </summary>
    private static readonly ILog Logger = Logs.GetLogger<DocumentsImporter>();

    /// <summary>
    /// Генератор случайных чисел.
    /// </summary>
    private static readonly Random random = new Random();

    /// <summary>
    /// Получить тип импортируемых документов.
    /// </summary>
    /// <param name="types">Набор типов.</param>
    /// <returns></returns>
    protected abstract ODataExpression GetDocumentType(ODataExpression types);

    /// <summary>
    /// Имя типа импортируемых документов.
    /// </summary>
    protected abstract string DocumentTypeName { get; }

    /// <summary>
    /// Настройки подключения к сервису интеграции.
    /// </summary>
    private IntegrationServiceSettings clientSettings;

    public DocumentsImporter(IntegrationServiceSettings clientSettings)
    {
      this.clientSettings = clientSettings;
    }

    /// <summary>
    /// Импорт документов.
    /// </summary>
    /// <param name="path">Путь до папки, откуда импортировать.</param>
    /// <param name="settings">Настройки загрузки документов.</param>
    /// <returns>Код возврата.</returns>
    public async Task<int> ImportFrom(string path, DocumentUploadSettings settings)
    {
      try
      {
        Logger.Debug("Start importing documents");

        var documentsFolder = this.GetDocumentsFolder(path);
        Logger.Debug($"Importing documents from folder {documentsFolder}:");

        var subfolders = Directory.GetDirectories(Path.GetFullPath(documentsFolder));
        if (!subfolders.Any())
          subfolders = new[] { documentsFolder };

        var agents = new List<Task<int>>();
        var timer = new Stopwatch();
        timer.Start();
        foreach (var subfolder in subfolders)
        {
          agents.Add(Task.Run(async () =>
          {
            var timeout = GetRandomTime(settings.AgentStartTimeout);
            await Task.Delay(timeout).ConfigureAwait(false);

            var client = IntegrationServiceClient.Create(this.clientSettings);
            if (client == null)
            {
              Logger.Error("Client for integration service is not created");
              return 0;
            }
            else
            {
              Logger.Debug($"Agent for folder \"{subfolder}\" started");
            }
            return await this.ImportDocuments(client, subfolder, settings).ConfigureAwait(false);
          }));
        }
        var documentsCount = (await Task.WhenAll(agents).ConfigureAwait(false)).Sum();

        timer.Stop();
        Logger.Debug($"End importing documents in {timer.Elapsed.ToString(@"hh\:mm\:ss")} time");
        Logger.Debug($"Total imported documents count is about: {documentsCount}");
      }
      catch (Exception ex)
      {
        HandleError(ex);
        return -1;
      }
      return 0;
    }

    /// <summary>
    /// Импортировать документы из папки.
    /// </summary>
    /// <param name="documentsFolder">Папка с документами.</param>
    /// <param name="settings">Настройки загрузки документов.</param>
    /// <returns>Количество импортированных документов.</returns>
    private async Task<int> ImportDocuments(ODataClient client, string documentsFolder, DocumentUploadSettings settings)
    {
      var files = Directory.EnumerateFiles(documentsFolder, "*.*", new EnumerationOptions { RecurseSubdirectories = true });
      if (settings.UploadOrder == DocumentUploadOrder.Sequential)
      {
        await this.ImportDocumentsSequential(client, files, settings).ConfigureAwait(false);
      }
      else if (settings.UploadOrder == DocumentUploadOrder.Parallel)
      {
        await this.ImportDocumentsParallel(client, files, settings).ConfigureAwait(false);
      }
      return files.Count();
    }

    /// <summary>
    /// Импортировать файлы в параллельном режиме загрузки порций.
    /// </summary>
    /// <param name="files">Набор файлов.</param>
    /// <param name="settings">Настройки загрузки документов.</param>
    private async Task ImportDocumentsParallel(ODataClient client, IEnumerable<string> files, DocumentUploadSettings settings)
    {
      const double TimeShift = 0.1;
      var portionIndex = 0;
      var uploadTasks = new List<Task>();
      var filePortions = settings.PortionSize > 0 ? files.SplitPages(settings.PortionSize) : new[] { files };
      foreach (var filesPortion in filePortions)
      {
        var timeout = portionIndex * settings.UploadPortionsInterval + GetRandomTime(settings.UploadPortionsInterval, TimeShift);
        uploadTasks.Add(Task.Run(async () =>
        {
          await Task.Delay(timeout).ConfigureAwait(false);
          if (settings.BatchSize > 0)
          {
            var uploadTasks = filesPortion.SplitPages(settings.BatchSize)
              .Select(async batch => await this.ImportDocumentsBatch(client, batch, settings.IsTraceEnabled).ConfigureAwait(false));

            await Task.WhenAll(uploadTasks).ConfigureAwait(false);
          }
          else
          {
            var uploadTasks = filesPortion
              .Select(async file => await this.ImportSingleDocument(client, file, settings.IsTraceEnabled).ConfigureAwait(false));

            await Task.WhenAll(uploadTasks).ConfigureAwait(false);
          }
        }));
        portionIndex++;
      }
      await Task.WhenAll(uploadTasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Импортировать файлы в последовательном режиме загрузки порций.
    /// </summary>
    /// <param name="files">Набор файлов.</param>
    /// <param name="settings">Настройки загрузки документов.</param>
    private async Task ImportDocumentsSequential(ODataClient client, IEnumerable<string> files, DocumentUploadSettings settings)
    {
      const double TimeShift = 0.1;
      TimeSpan getDelayBeforeUpload(bool isFirstItem)
      {
        if (isFirstItem)
          return GetRandomTime(settings.UploadInterval, TimeShift);
        else
          return settings.UploadInterval + GetRandomTime(settings.UploadInterval, TimeShift);
      }

      var portionIndex = 0;
      var uploadTasks = new List<Task>();
      var filePortions = settings.PortionSize > 0 ? files.SplitPages(settings.PortionSize) : new[] { files };
      foreach (var filesPortion in filePortions)
      {
        var timeout = portionIndex * settings.UploadPortionsInterval + GetRandomTime(settings.UploadPortionsInterval, TimeShift);
        uploadTasks.Add(Task.Run(async () =>
        {
          await Task.Delay(timeout).ConfigureAwait(false);
          if (settings.BatchSize > 0)
          {
            var isFirstBatch = true;
            foreach (var batch in filesPortion.SplitPages(settings.BatchSize))
            {
              await Task.Delay(getDelayBeforeUpload(isFirstBatch)).ConfigureAwait(false);
              await this.ImportDocumentsBatch(client, batch, settings.IsTraceEnabled).ConfigureAwait(false);
              isFirstBatch = false;
            }
          }
          else
          {
            var isFirstFile = true;
            foreach (var file in filesPortion)
            {
              await Task.Delay(getDelayBeforeUpload(isFirstFile)).ConfigureAwait(false);
              await this.ImportSingleDocument(client, file, settings.IsTraceEnabled).ConfigureAwait(false);
              isFirstFile = false;
            }
          }
        }));
        portionIndex++;
      }
      await Task.WhenAll(uploadTasks).ConfigureAwait(false);
    }

    #region Дискретный режим занесения

    /// <summary>
    /// Импортировать один документ (в дискретном режиме).
    /// </summary>
    /// <param name="documentPath">Путь до документа.</param>
    /// <param name="isTraceEnabled">Нужно ли логировать процесс загрузки.</param>
    private async Task ImportSingleDocument(ODataClient client, string documentPath, bool isTraceEnabled)
    {
      try
      {
        var documentInfo = this.GetDocumentInfo(documentPath);
        var documentClient = client.For(this.DocumentTypeName);
        // Создать документ.
        dynamic createdDocument = await this.CreateDocument(documentClient, documentInfo).ConfigureAwait(false);
        // Заполнить тело.
        await this.FillBody(documentClient, createdDocument, documentInfo).ConfigureAwait(false);

        if (isTraceEnabled)
          Logger.Debug($"Document \"{documentInfo.FullName}\" has been imported.");
      }
      catch (Exception ex)
      {
        HandleError(ex);
      }
    }

    /// <summary>
    /// Создать документ на сервере.
    /// </summary>
    /// <param name="documentInfo">Инфошка документа.</param>
    /// <returns>Созданный документ.</returns>
    private async Task<dynamic> CreateDocument(IDocumentClient client, DocumentInfo documentInfo)
    {
      // Заполнить свойства.
      client.Set(this.GetDocumentProperties(documentInfo));
      return await client.InsertEntryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Заполнить тело документа.
    /// </summary>
    /// <param name="document">Документ.</param>
    /// <param name="documentInfo">Инфошка документа.</param>
    private async Task FillBody(IDocumentClient client, dynamic document, DocumentInfo documentInfo)
    {
      if (documentInfo.Body.Length == 0)
        return;

      // Создание версии.
      var d = ODataDynamic.Expression;
      dynamic version = await client
         .Key(document)
         .NavigateTo(d.Versions)
         .Set(d.AssociatedApplication = new { Id = AssociatedApplications.GetRandomId() })
         .InsertEntryAsync()
         .ConfigureAwait(false);

      // Заполнение тела версии.
      await client
        .Key(document)
        .NavigateTo(d.Versions)
        .Key(version)
        .NavigateTo(d.Body)
        .Set(d.Value = documentInfo.Body)
        .InsertEntryAsync()
        .ConfigureAwait(false);
    }

    /// <summary>
    /// Заполнить свойства документа.
    /// </summary>
    /// <param name="documentInfo">Инфошка документа.</param>
    protected virtual dynamic GetDocumentProperties(DocumentInfo documentInfo)
    {
      dynamic props = new ExpandoObject();
      props.Name = documentInfo.FullName;
      return props;
    }

    #endregion

    #region Пакетный режим занесения

    /// <summary>
    /// Импортировать несколько документов (в пакетном режиме).
    /// </summary>
    /// <param name="documentPath">Пути до документов.</param>
    /// <param name="isTraceEnabled">Нужно ли логировать процесс загрузки.</param>
    private async Task ImportDocumentsBatch(ODataClient client, IEnumerable<string> documentPaths, bool isTraceEnabled)
    {
      try
      {
        var documentInfos = documentPaths.Select(d => GetDocumentInfo(d)).ToArray();
        await this.ImportDocumentsBatch(client, documentInfos).ConfigureAwait(false);

        if (isTraceEnabled)
        {
          if (documentInfos.Length == 1)
            Logger.Debug($"Document \"{documentInfos.Single().FullName}\" has been imported.");
          else
            Logger.Debug($"Documents has been imported:{Environment.NewLine}{string.Join(Environment.NewLine, documentInfos.Select(d => d.FullName))}{Environment.NewLine}");
        }
      }
      catch (Exception ex)
      {
        HandleError(ex);
      }
    }

    /// <summary>
    /// Импортировать пакет документов.
    /// </summary>
    /// <param name="documentInfos">Инфошки документов.</param>
    private async Task ImportDocumentsBatch(ODataClient client, IEnumerable<DocumentInfo> documentInfos)
    {
      var batch = new ODataBatch(client, true);

      var index = 0;
      foreach (var documentInfo in documentInfos)
      {
        index++;
        this.AddDocumentToBatch(batch, documentInfo, index);
      }

      await batch.ExecuteAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Добавить документ в пакет.
    /// </summary>
    /// <param name="batch">Пакет.</param>
    /// <param name="documentInfo">Инфошка документа.</param>
    /// <param name="index">Порядковый номер документа в пакете.</param>
    private void AddDocumentToBatch(ODataBatch batch, DocumentInfo documentInfo, int index)
    {
      var x = ODataDynamic.Expression;

      var docId = -2 * index + 1;
      var versionId = -2 * index;

      // Создание документа.
      var props = this.GetDocumentProperties(documentInfo);
      props.Id = docId;
      batch += async c => await c
        .For(this.DocumentTypeName)
        .Set(props)
        .InsertEntryAsync()
        .ConfigureAwait(false);

      // Создание версии.
      batch += async c => await c
        .For(this.DocumentTypeName)
        .Key(docId)
        .NavigateTo(x.Versions)
        .Set(new { Id = versionId, Number = 1, AssociatedApplication = new { Id = AssociatedApplications.GetRandomId() } })
        .InsertEntryAsync()
        .ConfigureAwait(false);

      // Заполнение тела версии.
      batch += async c => await c
        .For(this.DocumentTypeName)
        .Key(docId)
        .NavigateTo(x.Versions)
        .Key(versionId)
        .NavigateTo(x.Body)
        .Set(new { Value = documentInfo.Body })
        .InsertEntryAsync()
        .ConfigureAwait(false);
    }

    #endregion

    /// <summary>
    /// Получить информацию о документе.
    /// </summary>
    /// <param name="documentPath">Путь к документу.</param>
    /// <returns></returns>
    private DocumentInfo GetDocumentInfo(string documentPath)
    {
      var parentFolder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(documentPath), "..", ".."));
      var documentInfo = new DocumentInfo
      {
        Name = Path.GetFileNameWithoutExtension(documentPath),
        FullName = documentPath.Replace(parentFolder, "").Trim(Path.DirectorySeparatorChar),
        Extension = Path.GetExtension(documentPath).Trim('.')
      };

      if (File.Exists(documentPath))
        documentInfo.Body = File.ReadAllBytes(documentPath);
      else
        documentInfo.Body = new byte[0];

      return documentInfo;
    }

    /// <summary>
    /// Получить путь до конечной папки с документами.
    /// </summary>
    /// <param name="importFolder">Папка с документами.</param>
    /// <returns>Путь до конечной папки с документами.</returns>
    private string GetDocumentsFolder(string importFolder)
    {
      if (string.IsNullOrWhiteSpace(importFolder))
        throw new ArgumentException("Directory is null or empty.", nameof(importFolder));

      if (!Directory.Exists(importFolder))
        throw new ArgumentException("Directory does not exist.", nameof(importFolder));

      var documentsPath = ReplaceSpecialSymbols(importFolder);
      if (documentsPath == null || !Directory.Exists(documentsPath))
      {
        throw new ApplicationException(string.Format("Import path {0} is incorrect.", documentsPath));
      }
      return Path.GetFullPath(documentsPath);
    }

    /// <summary>
    /// Убрать лишние символы из пути к директории.
    /// </summary>
    /// <param name="path">Путь к директории.</param>
    /// <returns>Путь к директории без лишних символов.</returns>
    private static string ReplaceSpecialSymbols(string path)
    {
      if (path == null)
        return null;
      return path.Replace("'", string.Empty).Replace("\"", string.Empty).Trim();
    }

    /// <summary>
    /// Обработать ошибку.
    /// </summary>
    /// <param name="error">Ошибка.</param>
    private static void HandleError(Exception error)
    {
      var innerException = IntegrationServiceClient.GetSimpleODataInnerException(error);
      Logger.Log(LogLevel.Error, innerException, innerException.Message);
    }

    /// <summary>
    /// Получить случайное время в заданном промежутке времени.
    /// </summary>
    /// <param name="interval">Промежуток времени.</param>
    /// <param name="shift">Коэффициент смещения.</param>
    /// <returns></returns>
    private static TimeSpan GetRandomTime(TimeSpan interval, double shift = 1)
    {
      return TimeSpan.FromMilliseconds(random.Next(0, (int)(interval.TotalMilliseconds * shift)));
    }
  }
}
