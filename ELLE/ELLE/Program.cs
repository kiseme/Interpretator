using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELLE
{
    public enum ElementType
    {
        // Нетерминалы
        START,
        NEXT_PARAM,
        FUNC,
        SUB_FUNC,
        ARGS,
        BLOCK,
        TYPE_DESC,
        ZARR,
        NEXT_ARG,
        TYPE,
        STATEMENT,
        EXPR,
        DESC,
        INDEX,
        PARAM,
        MULT_EXPR,
        ADD_EXPR,
        COMP_EXPR,
        AND_EXPR,
        OR_EXPR,
        NEG,
        LEXPR,
        OR_TERM,
        AND_FACTOR,
        LCOMP_EXPR,
        TERM,
        FACTOR,
        CEXPR,
        Z,
        TYPEDEF,
        VALUE,
        ELSEST,
        EMPTY,
        FINISH,
        LTYPEDEF,
        ASS,
        // Терминалы
        BEGIN,
        END,
        BY,
        OF,
        ARRAY,
        INT,
        DOUBLE,
        BYTE,
        STRING,
        INT_CONST,
        DOUBLE_CONST,
        BYTE_CONST,
        STRING_CONST,
        NAME,
        WHILE,
        IF,
        ELSE,
        ASSIGN,
        PLUS,
        MINUS,
        MULT,
        DIVISION,
        OR,
        AND,
        NOT,
        LESS,
        GREATER,
        EQ,
        LEQ,
        GEQ,
        NEQ,
        STATEMENT_END, //;
        TRUE,
        FALSE,
        READ,
        WRITE,
        OPEN_FIGURE,
        CLOSE_FIGURE,
        OPEN_SQUARE,
        CLOSE_SQUARE,
        OPEN_BRACKET,
        CLOSE_BRACKET,
        NONE
    }

    public class Lexem
    {
        private string name;
        private ElementType type;
        private int linePos;
        private int simbolPos;

        public string Name { get { return name; } set { name = value; } }
        public ElementType Type { get { return type; } set { type = value; } }
        public int LinePos { get { return linePos; } set { linePos = value; } }
        public int SimbolPos { get { return simbolPos; } set { simbolPos = value; } }

    }

    public class Variable
    {

    }

    public class Number
    {
        private string name;
        private int meaning;

        public string Name { get { return name; } set { name = value; } }
        public int Meaning { get { return meaning; } set { meaning = value; } }
    }

    public class Array
    {
        private string name;
        private int[] meaning;

        public string Name { get { return name; } set { name = value; } }
        public int[] Meaning { get { return meaning; } set { meaning = value; } }
    }

    public class LexAn
    {
        public List<Lexem> lexems = new List<Lexem>();        // Набор ВСЕХ лексем
        public List<Number> numbers = new List<Number>();     // Переменные
        public List<Array> arrays = new List<Array>();  // Массивы

        //Добавление лексемы в массив
        private void AddNewLexemInLexems(string name, ElementType type, int nl, int ns)
        {
            lexems.Add(new Lexem()
            {
                Name = name,
                Type = type,
                LinePos = nl,
                SimbolPos = ns
            });
        }

        //Состояния
        public enum State
        {
            START,
            WORD,
            INTEGER,
            EXPRESSION,
            FINISH
        };

        //Ключевые слова
        Dictionary<string, ElementType> Keywords = new Dictionary<string, ElementType>
        {
            { "not",    ElementType.NOT},
            { "or",     ElementType.OR },
            { "and",    ElementType.AND },
            { "while",  ElementType.WHILE },
            { "read",   ElementType.READ },
            { "write",  ElementType.WRITE },
            { "if",     ElementType.IF },
            { "else",   ElementType.ELSE },
            { "begin",   ElementType.BEGIN },
            { "end",   ElementType.END },
            { "!=",   ElementType.NEQ },
            { ":=",   ElementType.ASSIGN },
        };

        //Проверка на ключевое слово
        private ElementType GetElementType(string name)
        {
            if (Keywords.TryGetValue(name, out ElementType it))
                return it;
            return ElementType.NAME; // по-умолчанию — имя переменной
        }

        //Номер колонки
        int GetCol(char c)
        {
            if (Char.IsLetter(c)) return 0;
            if (Char.IsDigit(c)) return 1;
            switch (c)
            {
                case '+': return 2;
                case '-': return 3;
                case '*': return 4;
                case '/': return 5;
                case '{': return 6;
                case '}': return 7;
                case '(': return 8;
                case ')': return 9;
                case '[': return 10;
                case ']': return 11;
                case ';': return 12;
                case '\t': return 13;
                case ' ': return 13;
                case '\r': return 13;
                case '\n': return 14;
                case '<': return 15;
                case '>': return 16;
                case ':': return 17;
                case '=': return 18;
                case '!': return 17;
                default: return 19;
            }
        }

        // Таблица переходов
        int[,] Programs = new int[,]
        {
                       //LET  DIG     +     -      *      /      {       }     (      )      [       ]      ;        \t     \n         <        >       :!       =           ERR
        /*START*/      {10,   20,     30,   40,    50,    60,    70,    80,    90,    100,   110,    120,   130,      0,     1,       11,       12,     13,     14,         400},
        /*WORD*/       {140,  140,    150,  150,  150,   150,   400,   400,   150,   150,   150,    150,    400,     150,    150,     150,      150,   150,    150,         400},
        /*INTEGER*/    {500,  160,    170,  170,   170,  170,   400,   170,   170,   170,   170,    170,    170,   170,     170,      170,      170,   170,    170,         400},
        /*EXPRESSIONS*/{400,  400,    400,  400,   400,  400,   400,   400,   400,   400,   400,    400,    400,   400,     400,     400,       400,   400,     15,         400},
        };

        int NumberSymbolNow = -1;     // Считываемый символ
        int NumberLineNow = 1;        // Считываемая линия
        int NumberSymbolOnLine = -1;   // Номер символа на строке
        int state = (int)State.START; // Состояние
        int NewNumber = 0;        // Новое считываемое число
        string NewLexemName = "";     // Новая лексема

        //Работа лексического анализатора
        public LexAn(string inputCode)
        {
            if (inputCode == "") return; // Пустой файл
            while (NumberSymbolNow + 1 < inputCode.Length)
            {
                state = (int)State.START;
                while (state != (int)State.FINISH && NumberSymbolNow + 1 < inputCode.Length)
                {

                    NumberSymbolNow++;
                    NumberSymbolOnLine++;

                    char symbolNow = inputCode[NumberSymbolNow];
                    switch (Programs[state, GetCol(symbolNow)])
                    {
                        case 0:
                            state = (int)State.START;
                            break;

                        #region <
                        case 1:
                            NumberLineNow++;
                            NumberSymbolOnLine = -1;
                            state = (int)State.START;
                            break;
                        #endregion

                        #region <
                        case 11:
                            AddNewLexemInLexems("<", ElementType.LESS, NumberLineNow, NumberSymbolOnLine);
                            state = (int)State.FINISH;
                            break;
                        #endregion

                        #region >
                        case 12:
                            AddNewLexemInLexems(">", ElementType.GREATER, NumberLineNow, NumberSymbolOnLine);
                            state = (int)State.FINISH;
                            break;
                        #endregion

                        #region !:
                        case 13:
                            NewLexemName = "" + symbolNow;
                            state = (int)State.EXPRESSION;
                            break;
                        #endregion

                        #region =
                        case 14:
                            AddNewLexemInLexems("=", ElementType.EQ, NumberLineNow, NumberSymbolOnLine);
                            state = (int)State.FINISH;
                            break;
                        #endregion

                        #region конец составного символа
                        case 15:
                            if (GetElementType(NewLexemName) == ElementType.NAME) { Console.WriteLine("unexpected lexem: line = " + NumberLineNow.ToString() + ", symbol = ", NumberSymbolOnLine.ToString()); }
                            else { AddNewLexemInLexems(NewLexemName, GetElementType(NewLexemName), NumberLineNow, NumberSymbolOnLine); }
                            NumberSymbolNow--;
                            NumberSymbolOnLine--;
                            break;
                        #endregion

                        #region первая буква
                        case 10:
                            NewLexemName = "" + symbolNow;
                            state = (int)State.WORD;
                            break;
                        #endregion

                        #region первая цифра
                        case 20:
                            NewNumber = (int)Char.GetNumericValue(symbolNow);
                            NewLexemName = "" + symbolNow;
                            state = (int)State.INTEGER;
                            break;
                        #endregion

                        #region +
                        case 30:
                            AddNewLexemInLexems("+", ElementType.PLUS, NumberLineNow, NumberSymbolOnLine);
                            state = (int)State.FINISH;
                            break;
                        #endregion

                        #region -
                        case 40:
                            AddNewLexemInLexems("-", ElementType.MINUS, NumberLineNow, NumberSymbolOnLine);
                            state = (int)State.FINISH;
                            break;
                        #endregion

                        #region *
                        case 50:
                            AddNewLexemInLexems("*", ElementType.MULT, NumberLineNow, NumberSymbolOnLine);
                            state = (int)State.FINISH;
                            break;
                        #endregion

                        #region /
                        case 60:
                            AddNewLexemInLexems("/", ElementType.DIVISION, NumberLineNow, NumberSymbolOnLine);
                            state = (int)State.FINISH;
                            break;
                        #endregion

                        #region {
                        case 70:
                            AddNewLexemInLexems("{", ElementType.OPEN_FIGURE, NumberLineNow, NumberSymbolOnLine);
                            state = (int)State.FINISH;
                            break;
                        #endregion

                        #region }
                        case 80:
                            AddNewLexemInLexems("}", ElementType.CLOSE_FIGURE, NumberLineNow, NumberSymbolOnLine);
                            state = (int)State.FINISH;
                            break;
                        #endregion

                        #region (
                        case 90:
                            AddNewLexemInLexems("(", ElementType.OPEN_BRACKET, NumberLineNow, NumberSymbolOnLine);
                            state = (int)State.FINISH;
                            break;
                        #endregion

                        #region )
                        case 100:
                            AddNewLexemInLexems(")", ElementType.CLOSE_BRACKET, NumberLineNow, NumberSymbolOnLine);
                            state = (int)State.FINISH;
                            break;
                        #endregion

                        #region [
                        case 110:
                            AddNewLexemInLexems("[", ElementType.OPEN_SQUARE, NumberLineNow, NumberSymbolOnLine);
                            state = (int)State.FINISH;
                            break;
                        #endregion

                        #region ]
                        case 120:
                            AddNewLexemInLexems("]", ElementType.CLOSE_SQUARE, NumberLineNow, NumberSymbolOnLine);
                            state = (int)State.FINISH;
                            break;
                        #endregion

                        #region ;
                        case 130:
                            AddNewLexemInLexems(";", ElementType.STATEMENT_END, NumberLineNow, NumberSymbolOnLine);
                            state = (int)State.FINISH;
                            break;
                        #endregion

                        #region ещё один символ
                        case 140:
                            NewLexemName += symbolNow;
                            state = (int)State.WORD;
                            break;
                        #endregion

                        #region конец слова
                        case 150:
                            AddNewLexemInLexems(NewLexemName, GetElementType(NewLexemName), NumberLineNow, NumberSymbolOnLine);
                            NumberSymbolNow--;
                            NumberSymbolOnLine--;
                            state = (int)State.FINISH;
                            break;
                        #endregion

                        #region ещё одна цифра в числе
                        case 160:
                            NewNumber *= 10;
                            NewNumber += (int)Char.GetNumericValue(symbolNow);
                            state = (int)State.INTEGER;
                            break;
                        #endregion

                        #region конец числа
                        case 170:
                            AddNewLexemInLexems(NewNumber.ToString(), ElementType.INT, NumberLineNow, NumberSymbolOnLine);
                            NumberSymbolNow--;
                            NumberSymbolOnLine--;
                            state = (int)State.FINISH;
                            break;
                        #endregion

                        #region ошибка unexpected lexem
                        case 400:
                            state = (int)State.FINISH;
                            Console.WriteLine("unexpected lexem: line = " + NumberLineNow.ToString() + ", symbol = ", NumberSymbolOnLine.ToString());
                            break;
                        #endregion

                        #region ошибка name starts with number
                        case 500:
                            state = (int)State.FINISH;
                            Console.WriteLine("name starts with number: line = " + NumberLineNow.ToString() + ", symbol = ", NumberSymbolOnLine.ToString());
                            break;
                            #endregion
                    }
                }

            }
            switch (state)
            {
                case (int)State.INTEGER: AddNewLexemInLexems(NewNumber.ToString(), ElementType.INT, NumberLineNow, NumberSymbolOnLine); break;
                case (int)State.WORD: AddNewLexemInLexems(NewLexemName, GetElementType(NewLexemName), NumberLineNow, NumberSymbolOnLine); break;
                case (int)State.EXPRESSION:
                    if (GetElementType(NewLexemName) == ElementType.NAME) { Console.WriteLine("unexpected lexem: line = " + NumberLineNow.ToString() + ", symbol = ", NumberSymbolOnLine.ToString()); }
                    else { AddNewLexemInLexems(NewLexemName, GetElementType(NewLexemName), NumberLineNow, NumberSymbolOnLine); }
                    break;
            }
            return;
        }
    }

    public class SynAn
    {

    }

    class Program
    {
        static void Main(string[] args)
        {
            string code = File.ReadAllText("test1.txt");
            LexAn lexem = new LexAn(code);

            //Результат работы лексического анализатора
            Console.WriteLine("Лексемы:\r\n");
            for (int i = 0; i < lexem.lexems.Count; i++)
            {
                Console.WriteLine(lexem.lexems[i].Name + " " +
                                  "Строка: " + lexem.lexems[i].LinePos + " " +
                                  "Номер символа в строке: " + lexem.lexems[i].SimbolPos + " " +
                                  "Тип: " + lexem.lexems[i].Type + "\r\n");
            }
            Console.WriteLine("\r\nПеременные:\r\n");
            for (int i = 0; i < lexem.numbers.Count; i++)
            {
                Console.WriteLine(lexem.numbers[i].Name + " - " + lexem.numbers[i].Meaning + "\r\n");
            }
            Console.WriteLine("Массивы:\r\n");
            for (int i = 0; i < lexem.arrays.Count; i++)
            {
                Console.WriteLine(lexem.arrays[i].Name + " - ");
                for (int j = 0; j < lexem.arrays[i].Meaning.Length; j++)
                {
                    Console.WriteLine(lexem.arrays[i].Meaning[j] + " ");
                }
            }
            Console.ReadLine();
        }
    }
}