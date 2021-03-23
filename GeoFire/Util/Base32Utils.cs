/*
 * Copyright 2019 Google LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * https://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Text.RegularExpressions;

namespace GeoFire.Util
{
    public static class Base32Utils {

        /* number of bits per base 32 character */
        public const int BitsPerBase32Char = 5;

        private const string Base32Chars = "0123456789bcdefghjkmnpqrstuvwxyz";

        public static char ValueToBase32Char(int value) 
        {
            if (value < 0 || value >= Base32Chars.Length) 
            {
                throw new ArgumentException("Not a valid base32 value: " + value);
            }
            return Base32Chars[value];
        }

        public static int Base32CharToValue(char base32Char) 
        {
            var value = Base32Chars.IndexOf(base32Char);
            if (value == -1) 
            {
                throw new ArgumentException("Not a valid base32 char: " + base32Char);
            }
            return value;
        }

        public static bool IsValidBase32String(string str)
        {
            var regex = new Regex("^[" + Base32Chars + "]*$");
            return regex.IsMatch(str);
        }
    }
}