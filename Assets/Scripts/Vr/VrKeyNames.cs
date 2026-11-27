using System.Collections.Generic;

namespace Vr
{
    public static class VrKeyNames
    {
        private static readonly Dictionary<string, string> Controller = new()
        {
            { "E", "Хват" },
            { "Q", "B" },
            { "G", "Відпусти хват" },
            { "ЛКМ", "Курок" },
            { "4", "Y" },
            { "1", "Пояс" },
            { "2", "Пояс" },
            { "3", "Пояс" },
            { "SPACE", "A" },
            { "ПРОБІЛ", "A" },
            { "SHIFT", "Лівий стік" },
            { "ESC", "Меню / X" },
        };

        public static string Translate(string key)
        {
            if (key == null)
            {
                return key;
            }

            return Controller.TryGetValue(key.Trim().ToUpperInvariant(), out string name) ? name : key;
        }
    }
}
