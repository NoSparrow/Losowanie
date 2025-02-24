using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.IO;
using System.Threading;

class Program
{
    // Globalne zmienne do przechowywania stanu
    static string filePath;
    static List<string> completeTableLines;
    static List<LottoResult> allResults;

    static async Task Main()
    {
        // Funkcja 1: Ustalenie lokalizacji pliku (3 opcje)
        if (!Function1_FileLocation()) return;
        if (!ContinuePromptDefault("Funkcja 1 zakończona. Wybierz: 1. Następny krok, 2. Wyjście")) return;

        // Opcja przed funkcją 2: Czy aktualizować dane?
        Console.WriteLine("Czy aktualizować dane?");
        Console.WriteLine("Wybierz: 1. Aktualizuj, 2. Pomiń aktualizację, 3. Wyjście");
        string updateOption = GetValidOption(new string[] { "1", "2", "3" });
        if (updateOption == "1")
        {
            await Function2_FetchAndSortData();
        }
        else if (updateOption == "2")
        {
            if (File.Exists(filePath))
            {
                completeTableLines = File.ReadAllLines(filePath).ToList();
                Console.WriteLine("Pominięto aktualizację – użyto danych z pliku.");
            }
            else
            {
                Console.WriteLine("Plik nie istnieje, a pominięto aktualizację danych. Program nie może kontynuować.");
                return;
            }
        }
        else // "3"
        {
            Console.WriteLine("Program zakończony.");
            return;
        }
        if (!ContinuePromptDefault("Funkcja 2 zakończona. Wybierz: 1. Następny krok, 2. Wyjście")) return;

        // Funkcja 3: Sprawdzenie zawartości pliku i ewentualna aktualizacja
        if (!Function3_CheckAndUpdateFile()) return;
        if (!ContinuePromptDefault("Funkcja 3 zakończona. Wybierz: 1. Następny krok, 2. Wyjście")) return;

        // Funkcje 4-10: Pozostałe funkcje – wyświetlają komunikat i pytają o przejście do następnej funkcji
        DomyślnaWariacjaiOdchylenie();
        if (!ContinuePromptCustom("Obliczanie domyślnych wartości wariacji i odchylenia standardowego zakończone. Wybierz: 1. Przejdź do funkcji 5, 2. Wyjście")) return;

        Function5_Dummy();
        if (!ContinuePromptCustom("Funkcja 5 zakończona. Wybierz: 1. Przejdź do funkcji 6, 2. Wyjście")) return;

        Function6_Dummy();
        if (!ContinuePromptCustom("Funkcja 6 zakończona. Wybierz: 1. Przejdź do funkcji 7, 2. Wyjście")) return;

        Function7_Dummy();
        if (!ContinuePromptCustom("Funkcja 7 zakończona. Wybierz: 1. Przejdź do funkcji 8, 2. Wyjście")) return;

        Function8_Dummy();
        if (!ContinuePromptCustom("Funkcja 8 zakończona. Wybierz: 1. Przejdź do funkcji 9, 2. Wyjście")) return;

        Function9_Dummy();
        if (!ContinuePromptCustom("Funkcja 9 zakończona. Wybierz: 1. Przejdź do funkcji 10, 2. Wyjście")) return;

        Function10_Dummy();
        Console.WriteLine("Program zakończony.");
    }

    // Funkcja pomocnicza: domyślny prompt (dla funkcji 1-3)
    static bool ContinuePromptDefault(string message)
    {
        Console.WriteLine(message);
        // Opcje: 1. Następny krok, 2. Wyjście
        string input = GetValidOption(new string[] { "1", "2" });
        return input == "1";
    }

    // Funkcja pomocnicza: prompt z niestandardowym komunikatem (dla funkcji 4-10)
    static bool ContinuePromptCustom(string message)
    {
        Console.WriteLine(message);
        // Opcje: 1. Przejdź do następnej funkcji, 2. Wyjście
        string input = GetValidOption(new string[] { "1", "2" });
        return input == "1";
    }

    // Funkcja pomocnicza: prosi użytkownika o wybór z opcji podanych w validOptions
    static string GetValidOption(string[] validOptions)
    {
        while (true)
        {
            string input = Console.ReadLine().Trim();
            if (validOptions.Contains(input))
                return input;
            Console.WriteLine("Niepoprawna odpowiedź. Proszę wybrać: " + string.Join(" lub ", validOptions));
        }
    }

    // Funkcja 1: Ustalenie lokalizacji pliku – 3 opcje: 1. Tak (domyślna), 2. Nie (podaj nową lokalizację), 3. Wyjście
    static bool Function1_FileLocation()
    {
        Console.WriteLine("Funkcja 1: Czy zachować domyślną lokalizację plików (/home/marcin/Pulpit/LottoCS/PobraneDane.txt)?");
        Console.WriteLine("Wybierz: 1. Tak, 2. Nie (podaj nową lokalizację), 3. Wyjście");
        string answer = GetValidOption(new string[] { "1", "2", "3" });
        string defaultPath = "/home/marcin/Pulpit/LottoCS/PobraneDane.txt";
        if (answer == "1")
        {
            filePath = defaultPath;
        }
        else if (answer == "2")
        {
            Console.WriteLine("Podaj nową lokalizację (pełna ścieżka, w tym nazwa pliku):");
            filePath = Console.ReadLine().Trim();
        }
        else // "3"
        {
            Console.WriteLine("Program zakończony.");
            return false;
        }
        // Sprawdzenie i utworzenie katalogu oraz pliku, jeśli nie istnieją
        string directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            Console.WriteLine("Utworzono katalog: " + directory);
        }
        if (!File.Exists(filePath))
        {
            File.Create(filePath).Close();
            Console.WriteLine("Utworzono plik: " + filePath);
        }
        else
        {
            Console.WriteLine("Plik już istnieje: " + filePath);
        }
        return true;
    }

    // Funkcja 2: Pobranie danych, sortowanie i budowa kompletnej tabeli wyników
    static async Task Function2_FetchAndSortData()
    {
        Console.WriteLine("Funkcja 2: Pobieranie danych ze wszystkich lat i budowa tabeli wyników...");
        // Pobierz linki do lat
        List<string> yearLinks = await FetchYearLinks();
        // Pobierz wyniki z każdego roku równolegle
        List<Task<List<LottoResult>>> tasks = new List<Task<List<LottoResult>>>();
        foreach (var link in yearLinks)
        {
            tasks.Add(Task.Run(() => FetchLottoResultsFromYear(link)));
        }
        var resultsByYear = await Task.WhenAll(tasks);
        allResults = resultsByYear.SelectMany(x => x).ToList();

        // Sortowanie wyników według daty (format dd-MM-yyyy)
        allResults.Sort((a, b) =>
        {
            DateTime da, db;
            if (DateTime.TryParseExact(a.Date, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out da) &&
                DateTime.TryParseExact(b.Date, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out db))
            {
                return da.CompareTo(db);
            }
            return a.Date.CompareTo(b.Date);
        });

        // Budowa tabeli z wewnętrzną numeracją (Lp.)
        completeTableLines = new List<string>();
        completeTableLines.Add("Lp.  | Data         | Zwycięska kombinacja");
        for (int i = 0; i < allResults.Count; i++)
        {
            var result = allResults[i];
            string combo = string.Join(" ", result.Numbers.Select(n => n.ToString().PadLeft(2)));
            string line = $"{(i + 1).ToString().PadRight(4)}| {result.Date.PadRight(12)}| {combo}";
            completeTableLines.Add(line);
        }
        Console.WriteLine("Dane pobrane, posortowane i tabela zbudowana. Łącznie wierszy (z nagłówkiem): " + completeTableLines.Count);
    }

    // Funkcja 3: Sprawdzenie zawartości pliku i ewentualna aktualizacja
    static bool Function3_CheckAndUpdateFile()
    {
        Console.WriteLine("Funkcja 3: Sprawdzanie zawartości pliku...");
        if (!File.Exists(filePath))
        {
            Console.WriteLine("Plik nie istnieje.");
            Console.WriteLine("Czy chcesz utworzyć nowy plik i zapisać dane? Wybierz: 1. Utwórz i kontynuuj, 2. Wyjście");
            string answer = GetValidOption(new string[] { "1", "2" });
            if (answer == "1")
            {
                File.WriteAllLines(filePath, completeTableLines);
                Console.WriteLine("Plik utworzony i dane zapisane.");
                return true;
            }
            else
            {
                Console.WriteLine("Program zakończony bez zapisywania.");
                return false;
            }
        }
        else
        {
            var fileLines = File.ReadAllLines(filePath).ToList();
            int differences = 0;
            int minCount = Math.Min(fileLines.Count, completeTableLines.Count);
            for (int i = 0; i < minCount; i++)
            {
                if (fileLines[i] != completeTableLines[i])
                    differences++;
            }
            differences += Math.Abs(fileLines.Count - completeTableLines.Count);
            if (differences > 0)
            {
                Console.WriteLine($"W pliku wykryto {differences} różnic.");
                Console.WriteLine("Czy chcesz nadpisać plik nowymi danymi? Wybierz: 1. Nadpisz i kontynuuj, 2. Wyjście");
                string answer = GetValidOption(new string[] { "1", "2" });
                if (answer == "1")
                {
                    File.WriteAllLines(filePath, completeTableLines);
                    Console.WriteLine("Plik został zaktualizowany.");
                    return true;
                }
                else
                {
                    Console.WriteLine("Program zakończony bez zapisywania.");
                    return false;
                }
            }
            else
            {
                Console.WriteLine("Plik jest aktualny.");
                return true;
            }
        }
    }

    // Funkcje 4-10
    static bool DomyślnaWariacjaiOdchylenie()
    {
        Console.WriteLine("Funkcja 4: Obliczanie teoretycznych wartości dla losowania lotto (6 liczb z zakresu 1-49)...");

        // Obliczenia teoretyczne
        double expectedValue = (1 + 49) / 2.0;  // Średnia = 25.00
        double variance = (Math.Pow(49, 2) - 1) / 12.0; // Wariancja = 200.00
        double stdDev = Math.Sqrt(variance); // Odchylenie standardowe ≈ 14.14

        // Przygotowanie tabeli wyników (header + jedna linia z danymi)
        List<string> metricsTable = new List<string>();
        metricsTable.Add("Oczekiwana wartość | Wariancja | Odchylenie standardowe");
        string row = string.Format("{0,18:F2} | {1,8:F2} | {2,24:F2}", expectedValue, variance, stdDev);
        metricsTable.Add(row);

        // Określenie ścieżki wyjściowej (w tym samym katalogu, co filePath)
        string outputFile = Path.Combine(Path.GetDirectoryName(filePath), "DomyślnaWariacjaiOdchylenie.txt");
        Console.WriteLine($"Plik docelowy: {outputFile}");

        // Jeśli plik już istnieje, zapytaj o akcję
        if (File.Exists(outputFile))
        {
            Console.WriteLine("Plik już istnieje. Wybierz: 1. Nadpisz i kontynuuj, 2. Pomiń ten krok, 3. Wyjście");
            string option = GetValidOption(new string[] { "1", "2", "3" });
            if (option == "1")
            {
                File.WriteAllLines(outputFile, metricsTable);
                Console.WriteLine("Plik został nadpisany nowymi danymi.");
            }
            else if (option == "2")
            {
                Console.WriteLine("Krok pominięty. Nie nadpisano pliku.");
            }
            else // "3"
            {
                Console.WriteLine("Program zakończony.");
                return false;
            }
        }
        else
        {
            File.WriteAllLines(outputFile, metricsTable);
            Console.WriteLine("Plik został utworzony z nowymi danymi.");
        }
        return true;
    }
    static void Function5_Dummy()
    {
        Console.WriteLine("To jest funkcja nr 5.");
    }
    static void Function6_Dummy()
    {
        Console.WriteLine("To jest funkcja nr 6.");
    }
    static void Function7_Dummy()
    {
        Console.WriteLine("To jest funkcja nr 7.");
    }
    static void Function8_Dummy()
    {
        Console.WriteLine("To jest funkcja nr 8.");
    }
    static void Function9_Dummy()
    {
        Console.WriteLine("To jest funkcja nr 9.");
    }
    static void Function10_Dummy()
    {
        Console.WriteLine("To jest funkcja nr 10.");
    }

    // Helper: Pobiera linki do lat ze strony głównej
    static async Task<List<string>> FetchYearLinks()
    {
        List<string> links = new List<string>();
        string url = "https://megalotto.pl/lotto/wyniki";
        HttpClient client = new HttpClient();
        var response = await client.GetStringAsync(url);
        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml(response);
        var nodes = doc.DocumentNode.SelectNodes("//p[contains(@class, 'lista_lat')]//a");
        if (nodes != null)
        {
            foreach (var node in nodes)
            {
                string href = node.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(href))
                    links.Add(href);
            }
        }
        return links;
    }

    // Helper: Pobiera wyniki Lotto z danej strony (dla konkretnego roku)
    static List<LottoResult> FetchLottoResultsFromYear(string yearUrl)
    {
        List<LottoResult> results = new List<LottoResult>();
        HttpClient client = new HttpClient();
        var response = client.GetStringAsync(yearUrl).Result;
        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml(response);
        var draws = doc.DocumentNode.SelectNodes("//div[@class='lista_ostatnich_losowan']/ul");
        if (draws != null)
        {
            foreach (var draw in draws)
            {
                var dateNode = draw.SelectSingleNode(".//li[contains(@class, 'date_in_list')]");
                var numberNodes = draw.SelectNodes(".//li[contains(@class, 'numbers_in_list')]");
                if (dateNode != null && numberNodes != null)
                {
                    List<int> numbers = numberNodes.Select(n => int.Parse(n.InnerText.Trim())).ToList();
                    results.Add(new LottoResult(0, dateNode.InnerText.Trim(), numbers));
                }
            }
        }
        return results;
    }
}

class LottoResult
{
    public int DrawNumber { get; }
    public string Date { get; }
    public List<int> Numbers { get; }
    public LottoResult(int drawNumber, string date, List<int> numbers)
    {
        DrawNumber = drawNumber;
        Date = date;
        Numbers = numbers;
    }
}
