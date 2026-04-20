namespace DictoriumDemo.Services;

using DictoriumDemo.Models;

public class DataStructureService
{
    private readonly List<DataStructureCategory> _categories;

    public DataStructureService()
    {
        _categories = new List<DataStructureCategory>
        {
            new DataStructureCategory
            {
                Id = StructureCategory.HashTable,
                Name = "Hash Tables",
                NameRu = "Хэш-таблицы",
                Description = "Структуры данных для хранения пар ключ-значение с быстрым доступом через хэш-функцию",
                Icon = "⬡",
                AccentColor = "#4ade80",
                Tag = "hashset",
                Structures = new List<DataStructureInfo>
                {
                    new DataStructureInfo
                    {
                        IssueNumber = 6,
                        Name = "Open Hashing",
                        NameRu = "Открытое хеширование",
                        Slug = "open-hashing",
                        Category = StructureCategory.HashTable,
                        Description = "Метод разрешения коллизий через цепочки (chaining). Каждая ячейка таблицы хранит список элементов с одинаковым хэшем.",
                        TimeComplexitySearch = "O(1) avg / O(n) worst",
                        TimeComplexityInsert = "O(1) avg",
                        TimeComplexityDelete = "O(1) avg",
                        SpaceComplexity = "O(n)",
                        Pros = new() { "Простота реализации", "Хорошо при высокой нагрузке", "Не требует перехэширования" },
                        Cons = new() { "Дополнительная память на указатели", "Плохая локальность кэша" },
                        UseCases = new() { "Словари", "Кэши", "Индексы баз данных" }
                    },
                    new DataStructureInfo
                    {
                        IssueNumber = 7,
                        Name = "Linear Probing",
                        NameRu = "Линейное пробирование",
                        Slug = "linear-probing",
                        Category = StructureCategory.HashTable,
                        Description = "Открытая адресация: при коллизии ищем следующую свободную ячейку линейно. h(k, i) = (h(k) + i) mod m.",
                        TimeComplexitySearch = "O(1) avg / O(n) worst",
                        TimeComplexityInsert = "O(1) avg",
                        TimeComplexityDelete = "O(1) avg",
                        SpaceComplexity = "O(n)",
                        Pros = new() { "Отличная локальность кэша", "Простота реализации", "Нет накладных расходов на указатели" },
                        Cons = new() { "Первичная кластеризация", "Деградация при высоком load factor" },
                        UseCases = new() { "Высокопроизводительные таблицы", "Встроенные системы" }
                    },
                    new DataStructureInfo
                    {
                        IssueNumber = 8,
                        Name = "Quadratic Probing",
                        NameRu = "Квадратичное пробирование",
                        Slug = "quadratic-probing",
                        Category = StructureCategory.HashTable,
                        Description = "Открытая адресация с квадратичным шагом: h(k, i) = (h(k) + c₁i + c₂i²) mod m.",
                        TimeComplexitySearch = "O(1) avg / O(n) worst",
                        TimeComplexityInsert = "O(1) avg",
                        TimeComplexityDelete = "O(1) avg",
                        SpaceComplexity = "O(n)",
                        Pros = new() { "Устраняет первичную кластеризацию", "Лучше линейного при высокой нагрузке" },
                        Cons = new() { "Вторичная кластеризация", "Не гарантирует обход всех ячеек" },
                        UseCases = new() { "Компиляторы", "Интерпретаторы" }
                    },
                    new DataStructureInfo
                    {
                        IssueNumber = 9,
                        Name = "Double Hashing",
                        NameRu = "Двойное хеширование",
                        Slug = "double-hashing",
                        Category = StructureCategory.HashTable,
                        Description = "Два хэша: шаг пробирования зависит от ключа. h(k, i) = (h₁(k) + i·h₂(k)) mod m.",
                        TimeComplexitySearch = "O(1) avg",
                        TimeComplexityInsert = "O(1) avg",
                        TimeComplexityDelete = "O(1) avg",
                        SpaceComplexity = "O(n)",
                        Pros = new() { "Минимальная кластеризация", "Равномерное распределение" },
                        Cons = new() { "Сложнее реализовать", "Два вычисления хэша" },
                        UseCases = new() { "Криптография", "Высоконагруженные приложения" }
                    },
                    new DataStructureInfo
                    {
                        IssueNumber = 10,
                        Name = "Robin Hood Hashing",
                        NameRu = "Robin Hood хеширование",
                        Slug = "robin-hood-hashing",
                        Category = StructureCategory.HashTable,
                        Description = "Вариант линейного пробирования: богатые (близкие к дому) уступают место бедным (далёким). Выравнивает расстояния смещения.",
                        TimeComplexitySearch = "O(1) avg",
                        TimeComplexityInsert = "O(1) avg",
                        TimeComplexityDelete = "O(1) avg",
                        SpaceComplexity = "O(n)",
                        Pros = new() { "Минимальная дисперсия времени поиска", "Хорошая производительность при высоком load factor" },
                        Cons = new() { "Сложнее реализовать", "Дополнительные сравнения при вставке" },
                        UseCases = new() { "Rust HashMap", "Высокопроизводительные системы" }
                    }
                }
            },

            new DataStructureCategory
            {
                Id = StructureCategory.Trees,
                Name = "Trees",
                NameRu = "Деревья (СУБД)",
                Description = "Иерархические структуры для эффективного хранения и поиска данных в системах управления базами данных",
                Icon = "⬟",
                AccentColor = "#a78bfa",
                Tag = "search in DBMS",
                Structures = new List<DataStructureInfo>
                {
                    new DataStructureInfo
                    {
                        IssueNumber = 2,
                        Name = "AVL Tree",
                        NameRu = "AVL дерево",
                        Slug = "avl-tree",
                        Category = StructureCategory.Trees,
                        Description = "Самобалансирующееся двоичное дерево поиска. Разница высот поддеревьев не более 1. Строго сбалансировано.",
                        TimeComplexitySearch = "O(log n)",
                        TimeComplexityInsert = "O(log n)",
                        TimeComplexityDelete = "O(log n)",
                        SpaceComplexity = "O(n)",
                        Pros = new() { "Строгий баланс — быстрый поиск", "Гарантированный O(log n)" },
                        Cons = new() { "Частые ротации при вставке/удалении", "Дополнительная память на высоту" },
                        UseCases = new() { "Базы данных с частым поиском", "Оперативная память" }
                    },
                    new DataStructureInfo
                    {
                        IssueNumber = 3,
                        Name = "Red-Black Tree",
                        NameRu = "Красно-чёрное дерево",
                        Slug = "red-black-tree",
                        Category = StructureCategory.Trees,
                        Description = "Самобалансирующееся двоичное дерево с дополнительным цветовым свойством. Менее строгий баланс, чем AVL.",
                        TimeComplexitySearch = "O(log n)",
                        TimeComplexityInsert = "O(log n)",
                        TimeComplexityDelete = "O(log n)",
                        SpaceComplexity = "O(n)",
                        Pros = new() { "Меньше ротаций, чем AVL", "Эффективные вставка и удаление", "Используется в std::map (C++)" },
                        Cons = new() { "Сложная реализация", "Чуть медленнее поиск, чем AVL" },
                        UseCases = new() { "std::map/set в C++", "TreeMap в Java", "Linux scheduler" }
                    },
                    new DataStructureInfo
                    {
                        IssueNumber = 4,
                        Name = "B-Tree",
                        NameRu = "В-дерево",
                        Slug = "b-tree",
                        Category = StructureCategory.Trees,
                        Description = "Многопутевое сбалансированное дерево поиска. Оптимизировано для дисковых операций. Все листья на одном уровне.",
                        TimeComplexitySearch = "O(log n)",
                        TimeComplexityInsert = "O(log n)",
                        TimeComplexityDelete = "O(log n)",
                        SpaceComplexity = "O(n)",
                        Pros = new() { "Минимизирует дисковые операции", "Хорошо для больших данных", "Поддержка диапазонных запросов" },
                        Cons = new() { "Сложная реализация", "Оверхед при малых данных" },
                        UseCases = new() { "Файловые системы", "Реляционные БД", "MongoDB" }
                    },
                    new DataStructureInfo
                    {
                        IssueNumber = 5,
                        Name = "B+ Tree",
                        NameRu = "В+-дерево",
                        Slug = "b-plus-tree",
                        Category = StructureCategory.Trees,
                        Description = "Расширение B-дерева: все данные хранятся в листьях, связанных в список. Внутренние узлы — только индексы.",
                        TimeComplexitySearch = "O(log n)",
                        TimeComplexityInsert = "O(log n)",
                        TimeComplexityDelete = "O(log n)",
                        SpaceComplexity = "O(n)",
                        Pros = new() { "Быстрый range scan через листья", "Лучше B-дерева для последовательного доступа", "Стандарт в СУБД" },
                        Cons = new() { "Дубликаты ключей во внутренних узлах", "Больше памяти" },
                        UseCases = new() { "MySQL InnoDB", "PostgreSQL", "SQLite" }
                    }
                }
            },

            new DataStructureCategory
            {
                Id = StructureCategory.AbsoluteO1,
                Name = "Absolute O(1)",
                NameRu = "Абсолют O(1)",
                Description = "Структуры данных с гарантированным O(1) в худшем случае для всех операций",
                Icon = "◈",
                AccentColor = "#fb923c",
                Tag = "Absolute O(1)",
                Structures = new List<DataStructureInfo>
                {
                    new DataStructureInfo
                    {
                        IssueNumber = 11,
                        Name = "Cuckoo Hashing",
                        NameRu = "Кукушкино хеширование",
                        Slug = "cuckoo-hashing",
                        Category = StructureCategory.AbsoluteO1,
                        Description = "Два хэша и две таблицы. При коллизии «выталкиваем» существующий элемент как кукушка — отсюда и название.",
                        TimeComplexitySearch = "O(1) worst",
                        TimeComplexityInsert = "O(1) amortized",
                        TimeComplexityDelete = "O(1) worst",
                        SpaceComplexity = "O(n)",
                        Pros = new() { "O(1) поиск в худшем случае", "Простой поиск (2 проверки)", "Высокий load factor" },
                        Cons = new() { "Возможны циклы при вставке", "Перехэширование при цикле", "Сложная реализация" },
                        UseCases = new() { "Сетевые маршрутизаторы", "Lookup-таблицы", "Кэши" }
                    },
                    new DataStructureInfo
                    {
                        IssueNumber = 12,
                        Name = "Perfect Hashing",
                        NameRu = "Совершенное хеширование",
                        Slug = "perfect-hashing",
                        Category = StructureCategory.AbsoluteO1,
                        Description = "Статическая хэш-таблица без коллизий для фиксированного множества ключей. Двухуровневая схема FKS.",
                        TimeComplexitySearch = "O(1) worst",
                        TimeComplexityInsert = "N/A (static)",
                        TimeComplexityDelete = "N/A (static)",
                        SpaceComplexity = "O(n)",
                        Pros = new() { "Идеальный O(1) поиск", "Нет коллизий", "Минимальное потребление памяти" },
                        Cons = new() { "Только для статических данных", "Долгое время построения", "Перестройка при изменении данных" },
                        UseCases = new() { "Компиляторы (ключевые слова)", "Статические словари", "DNS lookup" }
                    }
                }
            },

            new DataStructureCategory
            {
                Id = StructureCategory.ProbabilisticSearch,
                Name = "Probabilistic Search",
                NameRu = "Вероятностный поиск",
                Description = "Рандомизированные структуры данных с вероятностными гарантиями производительности",
                Icon = "◇",
                AccentColor = "#38bdf8",
                Tag = "Probabilistic search",
                Structures = new List<DataStructureInfo>
                {
                    new DataStructureInfo
                    {
                        IssueNumber = 13,
                        Name = "Skip List",
                        NameRu = "Список с пропусками",
                        Slug = "skip-list",
                        Category = StructureCategory.ProbabilisticSearch,
                        Description = "Многоуровневый связный список. Верхние уровни — «экспресс-маршруты» для быстрого поиска. Вероятностно сбалансирован.",
                        TimeComplexitySearch = "O(log n) expected",
                        TimeComplexityInsert = "O(log n) expected",
                        TimeComplexityDelete = "O(log n) expected",
                        SpaceComplexity = "O(n log n) expected",
                        Pros = new() { "Простая реализация по сравнению с деревьями", "Конкурентный доступ без ротаций", "Redis использует Skip List" },
                        Cons = new() { "Дополнительная память на уровни", "Нет O(log n) в worst case" },
                        UseCases = new() { "Redis Sorted Sets", "LevelDB", "Конкурентные структуры" }
                    },
                    new DataStructureInfo
                    {
                        IssueNumber = 14,
                        Name = "Cartesian Tree",
                        NameRu = "Декартово дерево",
                        Slug = "cartesian-tree",
                        Category = StructureCategory.ProbabilisticSearch,
                        Description = "Treap: комбинация BST по ключам и max-heap по приоритетам. Приоритеты случайны — дерево сбалансировано с высокой вероятностью.",
                        TimeComplexitySearch = "O(log n) expected",
                        TimeComplexityInsert = "O(log n) expected",
                        TimeComplexityDelete = "O(log n) expected",
                        SpaceComplexity = "O(n)",
                        Pros = new() { "Простые split/merge операции", "Ожидаемый O(log n)", "Элегантная реализация" },
                        Cons = new() { "Нет гарантий worst case", "Зависит от генератора случайных чисел" },
                        UseCases = new() { "Конкурентные алгоритмы", "RMQ (Range Minimum Query)", "Competitive programming" }
                    }
                }
            }
        };
    }

    public List<DataStructureCategory> GetAllCategories() => _categories;

    public DataStructureCategory? GetCategory(StructureCategory id) =>
        _categories.FirstOrDefault(c => c.Id == id);

    public DataStructureInfo? GetStructure(string slug) =>
        _categories.SelectMany(c => c.Structures).FirstOrDefault(s => s.Slug == slug);

    public List<DataStructureInfo> GetStructuresByCategory(StructureCategory category) =>
        _categories.FirstOrDefault(c => c.Id == category)?.Structures ?? new();

    public DataStructureCategory? GetCategoryForStructure(string slug)
    {
        foreach (var cat in _categories)
            if (cat.Structures.Any(s => s.Slug == slug))
                return cat;
        return null;
    }

    public List<DataStructureInfo> GetAllStructures() =>
        _categories.SelectMany(c => c.Structures).ToList();

    public string GetCategoryAccentColor(StructureCategory category) =>
        _categories.FirstOrDefault(c => c.Id == category)?.AccentColor ?? "#4ade80";
}
