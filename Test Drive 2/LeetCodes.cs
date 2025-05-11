using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test_Drive_2
{
	public class LeetCodes
	{
		public static int MajorityElement(List<int> list)
		{
			int count = 1;
			int element = list[0];
			for(int i=1; i<list.Count; i++)
			{
				if (list[i] == element)
				{
					count++;
				}
				else if (list[i] != element && count != 0)
				{
					count--;
				}
				else if (list[i] != element && count == 0)
				{
					element = list[i];
				}
			}
			return element;

		}
		public static int BestTimeToBuyAndSellAStock(List<int> stocks)
		{
			int minPrice = int.MaxValue;
			int maxPrice = 0;

			foreach(int price in stocks)
			{
				if(price < minPrice)
				{
					minPrice = price;// if the prices to decrese, then there is no time to sell					
				}
				else
				{
					maxPrice = Math.Max(maxPrice, price - minPrice);
				}
			}
			return maxPrice;
		}
	}
}
