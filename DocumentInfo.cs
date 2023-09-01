namespace Sungero.RxCmd
{
  /// <summary>
  /// Информация о документе.
  /// </summary>
  public struct DocumentInfo
  {
    /// <summary>
    /// Имя документа.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Полное имя документа (с учётом пути).
    /// </summary>
    public string FullName { get; set; }

    /// <summary>
    /// Расширение файла.
    /// </summary>
    public string Extension { get; set; }

    /// <summary>
    /// Тело документа.
    /// </summary>
    public byte[] Body { get; set; }
  }
}
