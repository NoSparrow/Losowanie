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
    // Zmienne globalne do przechowywania stanu
    static string filePath;
    static List<string> completeTableLines;
    static List<LottoResult> allResults;

    static async Task Main()
    {
        // Funkcja 1: Ustalenie lokalizacji pliku
        if (!Function1_FileLocation()) return;
        if (!ContinuePrompt()) return;

        // Funkcja 2: Pobranie danych, sortowanie i budowa tabeli (w formie listy stringów)
        await Function2_FetchAndSortData();
        if (!ContinuePrompt()) return;

        // Funkcja 3: Sprawdzenie pliku i porównanie zawartości – decyzja użytkownika
        if (!Function3_CheckAndUpdateFile()) return;
        if (!ContinuePrompt()) return;

        // Funkcje 4-10: Pozostałe funkcje – wyświetlają komunikat i pytają, czy kontynuować
        Function4_Dummy();
        if (!ContinuePrompt()) return;

        Function5_Dummy();
        if (!ContinuePrompt()) return;

        Function6_Dummy();
        if (!ContinuePrompt()) return;

        Function7_Dummy();
        if (!ContinuePrompt()) return;

        Function8_Dummy();
        if (!ContinuePrompt()) return;

        Function9_Dummy();
        if (!ContinuePrompt()) return;

        Function10_Dummy();
        Console.WriteLine("Program zakończony.");
    }

    // Funkcja pomocnicza: pyta, czy kontynuować działanie programu
    static bool ContinuePrompt()
    {
        Console.WriteLine("Czy kontynuować? (y/n)");
        string answer = Console.ReadLine();
        return answer.Trim().ToLower().StartsWith("y");
    }

    // Funkcja 1: Ustawienie lokalizacji pliku
    static bool Function1_FileLocation()
    {
        Console.WriteLine("Funkcja 1: Czy zachować domyślną lokalizację plików (/home/marcin/Pulpit/LottoCS/PobraneDane.txt)? (y/n)");
        string answer = Console.ReadLine();
        string defaultPath = "/home/marcin/Pulpit/LottoCS/PobraneDane.txt";
        if (answer.Trim().ToLower().StartsWith("y"))
            filePath = defaultPath;
        else
        {
            Console.WriteLine("Podaj nową lokalizację (pełna ścieżka, w tym nazwa pliku):");
            filePath = Console.ReadLine().Trim();
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

    // Funkcja 3: Sprawdzenie, czy plik istnieje i porównanie jego zawartości z nowymi danymi
    // Jeśli plik nie istnieje lub różni się – pyta, co zrobić (nadpisać, lub zakończyć program bez zapisywania)
    static bool Function3_CheckAndUpdateFile()
    {
        Console.WriteLine("Funkcja 3: Sprawdzanie zawartości pliku...");
        if (!File.Exists(filePath))
        {
            Console.WriteLine("Plik nie istnieje.");
            Console.WriteLine("Czy chcesz utworzyć nowy plik i zapisać dane? (y/n)");
            string answer = Console.ReadLine();
            if (answer.Trim().ToLower().StartsWith("y"))
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
                Console.WriteLine("Czy chcesz nadpisać plik nowymi danymi? (y/n)");
                string answer = Console.ReadLine();
                if (answer.Trim().ToLower().StartsWith("y"))
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

    // Funkcje 4-10: Funkcje pomocnicze wyświetlające komunikat z numerem funkcji
    static void Function4_Dummy()
    {
        Console.WriteLine("To jest funkcja nr 4.");
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

    // Helper: Pobiera wyniki Lotto z podanej strony (dla konkretnego roku)
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
