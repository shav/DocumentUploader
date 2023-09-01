using System.Collections.Generic;
using System.Linq;

namespace Sungero.RxCmd
{
  public static class LinqExtensions
  {
    /// <summary>
    /// Разбить последовательность на страницы.
    /// </summary>
    /// <typeparam name="T">Тип элементов последовательности.</typeparam>
    /// <param name="source">Последовательность.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <returns>Набор страниц.</returns>
    public static IEnumerable<IEnumerable<T>> SplitPages<T>(this IEnumerable<T> source, int pageSize)
    {
      while (source.Any())
      {
        yield return source.Take(pageSize).ToList();
        source = source.Skip(pageSize);
      }
    }
  }
}
