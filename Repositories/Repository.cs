using System;
using System.Collections.Generic;
using System.Linq;

namespace Sungero.RxCmd.Repositories
{
  /// <summary>
  /// Репозиторий с ИД-шниками прикладных сущностей.
  /// </summary>
  internal abstract class Repository
  {
    /// <summary>
    /// Название настройки в конфиге с ИД-шниками сущностей.
    /// </summary>
    protected abstract string IdsSettingName { get; }

    /// <summary>
    /// Генератор случайных чисел.
    /// </summary>
    private static readonly Random random = new Random();

    /// <summary>
    /// Список с ИД-шниками сущностей.
    /// </summary>
    protected readonly Lazy<IList<int>> ids;

    /// <summary>
    /// Получить случайный ИД сущности.
    /// </summary>
    /// <returns></returns>
    public int GetRandomId()
    {
      if (ids.Value.Count <= 1)
      {
        return ids.Value.SingleOrDefault();
      }
      return ids.Value[random.Next(0, ids.Value.Count)];
    }

    private IList<int> InitializeIds()
    {
      var rawIds = ConfigSettingsService.GetConfigSettingsValueByName(IdsSettingName);
      return ParseIds(rawIds);
    }

    private static IList<int> ParseIds(string rawIds)
    {
      const string TOKEN_DELIMITER = ",";
      const string RANGE_DELIMITER = "..";
      
      static int Length(Range r) => r.End.Value - r.Start.Value + 1;

      // Считаем, что строка имеет валидный формат, иначе падает ошибка.
      var tokens = rawIds.Split(TOKEN_DELIMITER, StringSplitOptions.RemoveEmptyEntries);
      var ranges = tokens
        .Where(t => t.Contains(RANGE_DELIMITER))
        .Select(t => t.Split(RANGE_DELIMITER, StringSplitOptions.RemoveEmptyEntries))
        .Select(t => new Range(int.Parse(t[0]), int.Parse(t[1])))
        .ToArray();
      var rangesLength = ranges.Sum(r => Length(r));
      var idTokens = tokens.Where(t => !t.Contains(RANGE_DELIMITER)).Select(t => int.Parse(t.Trim()));
      var idTokensCount = tokens.Length - ranges.Length;

      var result = new List<int>(rangesLength + idTokensCount);
      result.AddRange(idTokens);
      foreach(var range in ranges)
      {
        result.AddRange(Enumerable.Range(range.Start.Value, Length(range)));
      }
      return result;
    }

    protected Repository()
    {
      this.ids = new Lazy<IList<int>>(InitializeIds, true);
    }
  }
}
