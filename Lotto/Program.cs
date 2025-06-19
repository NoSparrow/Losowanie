using System;
using System.Collections.Concurrent;
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

        // Funkcja 3: Sprawdzenie zawartości pliku i ewentualna aktualizacja
        if (!Function3_CheckAndUpdateFile()) return;


        DomyślnaWariacjaiOdchylenie(); //Funkcja 4

        Function5_ProcessData();

        Function6_Dummy();

        Function7_Dummy();

        Function8_Dummy();

        Function9_Dummy();

        Function10_Dummy();
        Console.WriteLine("Program zakończony.");
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

    static bool Function5_ProcessData()
    {
        Console.WriteLine("Funkcja 5: Przetwarzanie danych sekwencyjne...");

        string folderPath = Path.GetDirectoryName(filePath);
        string processedFile = Path.Combine(folderPath, "Przetworzone.txt");
        string resultsFile = Path.Combine(folderPath, "Wyniki.txt");

        // Odczytaj dane z pliku PobraneDane.txt (pomijamy nagłówek)
        if (!File.Exists(filePath))
        {
            Console.WriteLine("Plik PobraneDane.txt nie istnieje. Przerywam przetwarzanie.");
            return false;
        }
        var allData = File.ReadAllLines(filePath).Skip(1).ToList();

        // Odczytaj już przetworzone losowania z Przetworzone.txt (pomijamy nagłówek, jeśli istnieje)
        HashSet<string> processedSet = new HashSet<string>();
        if (File.Exists(processedFile))
        {
            var procLines = File.ReadAllLines(processedFile).ToList();
            if (procLines.Count > 0)
                processedSet = new HashSet<string>(procLines.Skip(1));
        }
        else
        {
            File.Create(processedFile).Close();
        }

        // Wyznacz losowania, które nie zostały jeszcze przetworzone
        var unprocessed = allData.Where(line => !processedSet.Contains(line)).ToList();
        if (unprocessed.Count == 0)
        {
            Console.WriteLine("Brak nowych losowań do przetworzenia.");
            Console.WriteLine("Wybierz: 1. Przejdź dalej, 2. Wyjście");
            return GetValidOption(new string[] { "1", "2" }) == "1";
        }

        Console.WriteLine($"Do przetworzenia: {unprocessed.Count} losowań.");

        // Teoretyczne wartości dla losowania lotto: 6 liczb z zakresu 1-49
        double mean = 25.0;
        double stdDev = 14.1421356237; // dokładność do 10 miejsc

        List<string> resultLines = new List<string>();
        List<string> processedLines = new List<string>();

        // Przetwarzaj losowania jedno po drugim
        foreach (var line in unprocessed)
        {
            var parts = line.Split('|');
            string date = parts[0].Trim();
            // Zakładamy, że kombinacja liczb znajduje się w części po pierwszym '|' i są oddzielone spacjami
            var numbers = parts[1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(int.Parse).ToList();

            // Oblicz Z-score dla każdej liczby z dokładnością do 10 miejsc po przecinku
            var zScores = numbers.Select(n => Math.Round((n - mean) / stdDev, 10)).ToList();
            double avgZ = Math.Round(zScores.Average(), 10);
            double comboZ = Math.Round(zScores.Sum() / Math.Sqrt(zScores.Count), 10);

            // Format wynikowy: Data | Zwycięska Kombinacja | Z-score liczba1 | ... | Z-score średnia | Z-score kombinacji
            string result = $"{date} | {string.Join(" ", numbers)} | {string.Join(" | ", zScores.Select(z => z.ToString("F10")))} | {avgZ:F10} | {comboZ:F10}";
            resultLines.Add(result);
            processedLines.Add(line);

            Console.WriteLine($"Przetworzono losowanie: {date}");
            Console.WriteLine("Naciśnij ENTER, aby kontynuować lub wpisz 'stop', aby zakończyć przetwarzanie:");
            string input = Console.ReadLine().Trim().ToLower();
            if (input == "stop")
            {
                Console.WriteLine("Przerwano dalsze przetwarzanie losowań.");
                break;
            }
        }

        // Zapisz przetworzone wyniki do plików (dopisuje do istniejących)
        File.AppendAllLines(resultsFile, resultLines);
        File.AppendAllLines(processedFile, processedLines);

        Console.WriteLine("Przetwarzanie sekwencyjne zakończone.");
        Console.WriteLine("Wybierz: 1. Przejdź dalej, 2. Wyjście");
        return GetValidOption(new string[] { "1", "2" }) == "1";
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
