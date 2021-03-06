﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    static class Bit
    {
        public static bool IsSet(int flags, int bit)
        {
            return (flags & bit) != 0;
        }

        public static int Set(int flags, int bit)
        {
            return flags | bit;
        }

        public static int Clear(int flags, int bit)
        {
            return flags & (~bit);
        }
    }
}
