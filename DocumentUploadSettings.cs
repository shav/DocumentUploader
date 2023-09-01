using System;

namespace Sungero.RxCmd
{
  /// <summary>
  /// Порядок загрузки документов в порции.
  /// </summary>
  public enum DocumentUploadOrder
  {
    /// <summary>
    /// Параллельный.
    /// </summary>
    Parallel,

    /// <summary>
    /// Последовательный.
    /// </summary>
    Sequential,
  }

  /// <summary>
  /// Настройки загрузки документов.
  /// </summary>
  public class DocumentUploadSettings
  {
    /// <summary>
    /// Время, за которое должны стартовать все агенты.
    /// </summary>
    public TimeSpan AgentStartTimeout { get; set; }

    /// <summary>
    /// Порядок загрузки документов в порции.
    /// </summary>
    public DocumentUploadOrder UploadOrder { get; set; }

    /// <summary>
    /// Размер порции загружаемых документов.
    /// </summary>
    public int PortionSize { get; set; }

    /// <summary>
    /// Интервал времени между отправками порций документов в одном агенте.
    /// </summary>
    public TimeSpan UploadPortionsInterval { get; set; }

    /// <summary>
    /// Интервал времени между отправкой документов внутри порций (только в последовательном режиме).
    /// </summary>
    public TimeSpan UploadInterval { get; set; }

    /// <summary>
    /// Нужно ли логировать процесс загрузки документов?
    /// </summary>
    public bool IsTraceEnabled { get; set; } = true;

    /// <summary>
    /// Размер пакета документов для загрузки в пакетном режиме
    /// (0 - если нужно использовать дискретный режим вместо пакетного).
    /// </summary>
    public int BatchSize { get; set; }

    public DocumentUploadSettings()
    {
    }
  }
}
