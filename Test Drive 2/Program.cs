// See https://aka.ms/new-console-template for more information
using Test_Drive_2;

Console.WriteLine("Hello, World!");
List<int> n = new List<int> { 2, 2, 1, 1, 1,1,1, 2, 2,6,6,6,6,6,6,6,6,6 };
List<int> stocks = new List<int> { 2, 4, 1, 3, 2, 2, 8, 1, 5, 2 };
var result = LeetCodes.MajorityElement(n);
//Console.WriteLine(result);
Console.WriteLine(LeetCodes.BestTimeToBuyAndSellAStock(stocks));
