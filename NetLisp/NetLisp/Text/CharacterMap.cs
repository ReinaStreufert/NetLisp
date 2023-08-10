using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Text
{
    public class CharacterMap<T>
    {
        public List<CharacterRange> Ranges { get; set; } = new List<CharacterRange>();
        public List<CharacterRange> RangeMap { get; set; } = new List<CharacterRange>();

        public int Length { get; set; } = 0;

        public void Write(char c, T classification, bool seperate = false)
        {
            // does not do anything with c but it used to and i dont feel like changing all the references
            // i will at some point
            if (!seperate && Ranges.Count > 0 && Ranges[Ranges.Count - 1].Classification.Equals(classification))
            {
                Ranges[Ranges.Count - 1].Length++;
            } else
            {
                Ranges.Add(new CharacterRange(Length, 1, classification));
            }
            RangeMap.Add(Ranges[Ranges.Count - 1]);
            Length++;
        }

        public CharacterRange this[int position]
        {
            get
            {
                return RangeMap[position];
            }
        }

        public class CharacterRange
        {
            public int Start { get; set; }
            public int Length { get; set; }
            public T Classification { get; set; }
            public CharacterRange(int start, int length, T classification)
            {
                Start = start;
                Length = length;
                Classification = classification;
            }
        }
    }
}
