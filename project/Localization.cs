using System.Collections.Generic;

public static class Localization
{
    private static readonly Dictionary<string, string> _ru = new()
    {
        // Стадии роста
        { "Fry", "Малёк" },
        { "Teen", "Подросток" },
        { "Adult", "Взрослая" },

        // Редкость
        { "Common", "Обычная" },
        { "Rare", "Редкая" },
        { "Unique", "Уникальная" },

        // Голод
        { "Fed", "Сытая" },
        { "Hungry", "Голодная" },
        { "Starving", "Голодает!" },

        // Панель инфо
        { "Species", "Вид" },
        { "Stage", "Стадия" },
        { "Age", "Возраст" },
        { "Income", "Доход" },
        { "Hunger", "Голод" },
        { "Mutations", "Мутации" },
        { "None", "Нет" },
        { "Close", "Закрыть" },
        { "Rename", "Переименовать" },
        
        { "Glutton", "Обжора" },
        { "Predator", "Хищник" }
    };

    public static string T(string key)
    {
        if (_ru.TryGetValue(key, out var value))
            return value;

        return key; 
    }
}