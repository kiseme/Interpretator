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
        START,
        BOOL,
        BEGIN,
        END,
        ARRAY,
        INT,
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
        READ,
        WRITE,
        OPEN_FIGURE,
        CLOSE_FIGURE,
        OPEN_SQUARE,
        CLOSE_SQUARE,
        OPEN_BRACKET,
        CLOSE_BRACKET,
        IF_WHILE,
        IN_WHILE,
        NONE
    }

    public class Lexem
    {
        private string name;
        private ElementType type;
        private int linePos;
        private int simbolPos;
        private int _index = -1;


        public string Name { get { return name; } set { name = value; } }
        public ElementType Type { get { return type; } set { type = value; } }
        public int LinePos { get { return linePos; } set { linePos = value; } }
        public int SimbolPos { get { return simbolPos; } set { simbolPos = value; } }
        public int Index { get { return _index; } set { _index = value; } }
    }

    public class LexemString
    {
        private List<Lexem> _string;
        public List<Lexem> String { get { return _string; } set { _string = value; } }
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

    public class While
    {
        List<LexemString> _strings;
        bool _active = false;
        bool _bracetsIsClosed = false;
        int _index;

        public List<LexemString> Strings { get { return _strings; } set { _strings = value; } }
        public bool Active { get { return _active; } set { _active = value; } }
        public bool BracketsIsClosed { get { return _bracetsIsClosed; } set { _bracetsIsClosed = value; } }
        public int Index { get { return _index; } set { _index = value; } }
    }

    public class LexAn
    {
        public List<Lexem> Lexems { get { return lexems; } }
        public List<Number> Numbers { get { return numbers; } }
        public List<Array> Arrays { get { return arrays; } }
        public List<Lexem> lexems = new List<Lexem>();        // Набор ВСЕХ лексем
        public List<Number> numbers = new List<Number>();     // Переменные
        public List<Array> arrays = new List<Array>();        // Массивы

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
        string NewVariableName = "";  // Имя новой переменной

        // Нахождение переменной по ее имени
        private int FindVariable(string name)
        {
            for (int i = 0; i < numbers.Count; i++)
            {
                if (numbers[i].Name == name)
                {
                    return i;
                }
            }
            return -1;
        }

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
                            NewLexemName = NewLexemName + symbolNow;
                            if (GetElementType(NewLexemName) == ElementType.NAME) { Console.WriteLine("unexpected lexem: line = " + NumberLineNow.ToString() + ", symbol = ", NumberSymbolOnLine.ToString()); }
                            else { AddNewLexemInLexems(NewLexemName, GetElementType(NewLexemName), NumberLineNow, NumberSymbolOnLine); }
                            //NumberSymbolNow--;
                            //NumberSymbolOnLine--;
                            state = (int)State.FINISH;
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
                            if (GetElementType(NewLexemName) == ElementType.NAME)
                                numbers.Add(new Number() { Name = NewLexemName, Meaning = 0 });
                            NewVariableName = NewLexemName;
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
                            numbers[FindVariable(NewVariableName)].Meaning = NewNumber;
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

    class SyntaxAnalizator
    {
        public List<LexemString> OPS { get { return _OPS; } }

        public SyntaxAnalizator(List<Lexem> lexems)
        {
            CreateTemplateForOPS(lexems);

            for (int i = 0; i < _originalLexems.Count; i++)
            {
                _OPS.Add(new LexemString() { String = CreateOPS(_originalLexems[i].String) });
            }

            return;
        }

        List<LexemString> _originalLexems = new List<LexemString>(); // Оригинальные строки лексем
        List<LexemString> _OPS = new List<LexemString>();            // Набор преобразованных в ОПС строк

        // Проверка на то, является ли лексема последней в строке
        public bool LexemIsLast(LexemString lexString, Lexem lex)
        {
            if (lexString.String[lexString.String.Count - 1].Name == lex.Name)
            {
                return true;
            }

            return false;
        }

        // Приоритет символа
        private int GetPriority(ElementType type)
        {
            switch (type)
            {
                case ElementType.INT: return 0;
                case ElementType.NAME: return 0;

                case ElementType.MULT: return 2;
                case ElementType.DIVISION: return 2;

                case ElementType.PLUS: return 3;
                case ElementType.MINUS: return 3;

                case ElementType.OPEN_SQUARE: return 4;
                case ElementType.CLOSE_SQUARE: return 4;

                case ElementType.LESS: return 5;
                case ElementType.GREATER: return 5;
                case ElementType.EQ: return 5;
                //case ElementType.LESSOrEqual: return 5;
                //case ElementType.GREATEROrEqual: return 5;
                case ElementType.NEQ: return 5;

                case ElementType.AND: return 6;
                case ElementType.OR: return 6;
                case ElementType.NOT: return 6;

                case ElementType.OPEN_BRACKET: return 7;
                case ElementType.CLOSE_BRACKET: return 7;

                case ElementType.IF: return 8;
                case ElementType.WHILE: return 8;
                case ElementType.ASSIGN: return 8;
                case ElementType.WRITE: return 8;
                case ElementType.READ: return 8;

                default: return 9;
            }
        }

        // Преобразование лексем для обработки в вид ОПС
        // Здесь происходит создание списка списков лесем — матрица лексем
        private void CreateTemplateForOPS(List<Lexem> lexems)
        {
            bool programIsBegin = false;
            int numberOfLine = 1;
            List<Lexem> line = new List<Lexem>();
            for (int i = 0; i < lexems.Count; i++)
            {
                if (!programIsBegin)
                {
                    if (lexems[i].Name == "begin")
                    {
                        programIsBegin = true;
                        numberOfLine = lexems[i].LinePos + 1;
                    }
                    //дописать исключение
                }
                else
                {
                    if (lexems[i].Name != "EOF")
                    {
                        if (numberOfLine == lexems[i].LinePos)
                        {

                            line.Add(lexems[i]);
                        }
                        else
                        {
                            _originalLexems.Add(new LexemString() { String = line });
                            line = new List<Lexem>();
                            line.Add(lexems[i]);
                            numberOfLine = lexems[i].LinePos;
                        }
                    }
                }
            }
        }

        // Составление ОПС
        private List<Lexem> CreateOPS(List<Lexem> original)
        {
            Stack<Lexem> stackOperations = new Stack<Lexem>();
            List<Lexem> tempOPS = new List<Lexem>();

            Lexem tempForBracket;      // Переменная для проверки вытащенного элемента из стека
            int tempLengthOfStack = 0; // Первоначальная длина стека до первого .Pop();

            //операции для генерации стека-опс
            for (int i = 0; i < original.Count; i++)
            {
                switch (original[i].Type)
                {
                    #region Name
                    case ElementType.NAME:
                        tempOPS.Add(original[i]);
                        break;
                    #endregion

                    #region NUM
                    case ElementType.INT:
                        tempOPS.Add(original[i]);
                        break;
                    #endregion

                    #region If
                    case ElementType.IF:
                        stackOperations.Push(original[i]);
                        break;
                    #endregion

                    #region Else 
                    case ElementType.ELSE:
                        stackOperations.Push(original[i]);
                        break;
                    #endregion

                    #region While
                    case ElementType.WHILE:
                        stackOperations.Push(original[i]);
                        break;
                    #endregion

                    #region Read
                    case ElementType.READ:
                        stackOperations.Push(original[i]);
                        break;
                    #endregion

                    #region Write
                    case ElementType.WRITE:
                        stackOperations.Push(original[i]);
                        break;
                    #endregion

                    #region LeftFigurBracket
                    case ElementType.OPEN_FIGURE:
                        tempOPS.Add(original[i]);
                        break;
                    #endregion

                    #region RightFigurBracket
                    case ElementType.CLOSE_FIGURE:
                        tempOPS.Add(original[i]);
                        break;
                    #endregion

                    #region LeftRoundBracket
                    case ElementType.OPEN_BRACKET:
                        stackOperations.Push(original[i]);
                        break;
                    #endregion

                    #region RightRoundBracket
                    case ElementType.CLOSE_BRACKET:
                        do
                        {
                            tempForBracket = stackOperations.Pop();
                            if (tempForBracket.Type != ElementType.OPEN_BRACKET)
                            {
                                tempOPS.Add(tempForBracket);
                            }
                            else
                            {
                                break;
                            }
                        }
                        while (true);
                        break;
                    #endregion

                    #region LeftSquareBracket
                    case ElementType.OPEN_SQUARE:
                        stackOperations.Push(original[i]);
                        break;
                    #endregion

                    #region RightSquareBracket
                    case ElementType.CLOSE_SQUARE:
                        do
                        {
                            tempForBracket = stackOperations.Pop();
                            if (tempForBracket.Type != ElementType.OPEN_SQUARE)
                            {
                                tempOPS.Add(tempForBracket);
                            }
                            else
                            {
                                tempOPS.Add(new Lexem() { Name = "<index>", Type = ElementType.NONE });
                                break;
                            }
                        }
                        while (true);
                        break;
                    #endregion

                    #region And 
                    case ElementType.AND:
                        tempLengthOfStack = stackOperations.Count;
                        for (int j = 0; j < tempLengthOfStack; j++)
                        {
                            tempForBracket = stackOperations.Pop();
                            if (GetPriority(ElementType.AND) > GetPriority(tempForBracket.Type))
                            {
                                tempOPS.Add(tempForBracket);
                            }
                            else
                            {
                                stackOperations.Push(tempForBracket);
                                stackOperations.Push(original[i]);
                                break;
                            }
                        }
                        break;
                    #endregion

                    #region Or
                    case ElementType.OR:
                        tempLengthOfStack = stackOperations.Count;
                        for (int j = 0; j < tempLengthOfStack; j++)
                        {
                            tempForBracket = stackOperations.Pop();
                            if (GetPriority(ElementType.OR) > GetPriority(tempForBracket.Type))
                            {
                                tempOPS.Add(tempForBracket);
                            }
                            else
                            {
                                stackOperations.Push(tempForBracket);
                                stackOperations.Push(original[i]);
                                break;
                            }
                        }
                        break;
                    #endregion

                    #region Not
                    case ElementType.NOT:
                        tempLengthOfStack = stackOperations.Count;
                        for (int j = 0; j < tempLengthOfStack; j++)
                        {
                            tempForBracket = stackOperations.Pop();
                            if (GetPriority(ElementType.NOT) > GetPriority(tempForBracket.Type))
                            {
                                tempOPS.Add(tempForBracket);
                            }
                            else
                            {
                                stackOperations.Push(tempForBracket);
                                stackOperations.Push(original[i]);
                                break;
                            }
                        }
                        break;
                    #endregion

                    #region Less
                    case ElementType.LESS:
                        tempLengthOfStack = stackOperations.Count;
                        for (int j = 0; j < tempLengthOfStack; j++)
                        {
                            tempForBracket = stackOperations.Pop();
                            if (GetPriority(ElementType.LESS) > GetPriority(tempForBracket.Type))
                            {
                                tempOPS.Add(tempForBracket);
                            }
                            else
                            {
                                stackOperations.Push(tempForBracket);
                                stackOperations.Push(original[i]);
                                break;
                            }
                        }
                        break;
                    #endregion

                    #region Greater
                    case ElementType.GREATER:
                        tempLengthOfStack = stackOperations.Count;
                        for (int j = 0; j < tempLengthOfStack; j++)
                        {
                            tempForBracket = stackOperations.Pop();
                            if (GetPriority(ElementType.GREATER) > GetPriority(tempForBracket.Type))
                            {
                                tempOPS.Add(tempForBracket);
                            }
                            else
                            {
                                stackOperations.Push(tempForBracket);
                                stackOperations.Push(original[i]);
                                break;
                            }
                        }
                        break;
                    #endregion

                    #region Equal
                    case ElementType.EQ:
                        tempLengthOfStack = stackOperations.Count;
                        for (int j = 0; j < tempLengthOfStack; j++)
                        {
                            tempForBracket = stackOperations.Pop();
                            if (GetPriority(ElementType.EQ) > GetPriority(tempForBracket.Type))
                            {
                                tempOPS.Add(tempForBracket);
                            }
                            else
                            {
                                stackOperations.Push(tempForBracket);
                                stackOperations.Push(original[i]);
                                break;
                            }
                        }
                        break;
                    #endregion

                    #region LessOrEqual
                    /*case ElementType.LESSOrEqual:
                        tempLengthOfStack = stackOperations.Count;
                        for (int j = 0; j < tempLengthOfStack; j++)
                        {
                            tempForBracket = stackOperations.Pop();
                            if (GetPriority(ElementType.LESSOrEqual) > GetPriority(tempForBracket.Type))
                            {
                                tempOPS.Add(tempForBracket);
                            }
                            else
                            {
                                stackOperations.Push(tempForBracket);
                                stackOperations.Push(original[i]);
                                break;
                            }
                        }
                        break;*/
                    #endregion

                    #region GreaterOrEqual
                    /*case ElementType.GREATEROrEqual:
                        tempLengthOfStack = stackOperations.Count;
                        for (int j = 0; j < tempLengthOfStack; j++)
                        {
                            tempForBracket = stackOperations.Pop();
                            if (GetPriority(ElementType.GREATEROrEqual) > GetPriority(tempForBracket.Type))
                            {
                                tempOPS.Add(tempForBracket);
                            }
                            else
                            {
                                stackOperations.Push(tempForBracket);
                                stackOperations.Push(original[i]);
                                break;
                            }
                        }
                        break;*/
                    #endregion

                    #region NotEqual
                    case ElementType.NEQ:
                        tempLengthOfStack = stackOperations.Count;
                        for (int j = 0; j < tempLengthOfStack; j++)
                        {
                            tempForBracket = stackOperations.Pop();
                            if (GetPriority(ElementType.NEQ) > GetPriority(tempForBracket.Type))
                            {
                                tempOPS.Add(tempForBracket);
                            }
                            else
                            {
                                stackOperations.Push(tempForBracket);
                                stackOperations.Push(original[i]);
                                break;
                            }
                        }
                        break;
                    #endregion

                    #region Plus +
                    case ElementType.PLUS:
                        tempLengthOfStack = stackOperations.Count;
                        for (int j = 0; j < tempLengthOfStack; j++)
                        {
                            tempForBracket = stackOperations.Pop();
                            if (GetPriority(ElementType.PLUS) > GetPriority(tempForBracket.Type))
                            {
                                tempOPS.Add(tempForBracket);
                            }
                            else
                            {
                                stackOperations.Push(tempForBracket);
                                stackOperations.Push(original[i]);
                                break;
                            }
                        }
                        break;
                    #endregion

                    #region Minus -
                    case ElementType.MINUS:
                        tempLengthOfStack = stackOperations.Count;
                        for (int j = 0; j < tempLengthOfStack; j++)
                        {
                            tempForBracket = stackOperations.Pop();
                            if (GetPriority(ElementType.MINUS) > GetPriority(tempForBracket.Type))
                            {
                                tempOPS.Add(tempForBracket);
                            }
                            else
                            {
                                stackOperations.Push(tempForBracket);
                                stackOperations.Push(original[i]);
                                break;
                            }
                        }
                        break;
                    #endregion

                    #region Divide /
                    case ElementType.DIVISION:
                        tempLengthOfStack = stackOperations.Count;
                        for (int j = 0; j < tempLengthOfStack; j++)
                        {
                            tempForBracket = stackOperations.Pop();
                            if (GetPriority(ElementType.DIVISION) > GetPriority(tempForBracket.Type))
                            {
                                tempOPS.Add(tempForBracket);
                            }
                            else
                            {
                                stackOperations.Push(tempForBracket);
                                stackOperations.Push(original[i]);
                                break;
                            }
                        }
                        break;
                    #endregion

                    #region Multiply *
                    case ElementType.MULT:
                        tempLengthOfStack = stackOperations.Count;
                        for (int j = 0; j < tempLengthOfStack; j++)
                        {
                            tempForBracket = stackOperations.Pop();
                            if (GetPriority(ElementType.MULT) > GetPriority(tempForBracket.Type))
                            {
                                tempOPS.Add(tempForBracket);
                            }
                            else
                            {
                                stackOperations.Push(tempForBracket);
                                stackOperations.Push(original[i]);
                                break;
                            }
                        }
                        break;
                    #endregion

                    #region Assign :=
                    case ElementType.ASSIGN:
                        stackOperations.Push(original[i]);
                        break;
                        #endregion
                }
            }

            // В конце строки выбрасываем остатки из стека
            tempLengthOfStack = stackOperations.Count;
            for (int j = 0; j < tempLengthOfStack; j++)
            {
                tempOPS.Add(stackOperations.Pop());
            }

            return tempOPS;
        }
    }

    class Interpretator
    {
        public List<Number> Numbers { get { return _numbers; } set { _numbers = value; } }
        public List<Array> Massives { get { return _massives; } set { _massives = value; } }

        List<Number> _numbers;      // Переменные, используемые программой
        List<Array> _massives;    // Массивы, используемые программой
        Lexem _tempLexem;           // Временная лексема для хранения кончесного результата
        bool _onlyRead = false;     // Режим только для чтения

        // Старт интепретатора
        public Interpretator(ref List<Number> numbers, ref List<Array> massives)
        {
            _numbers = numbers;
            _massives = massives;
            return;
        }

        // Подготовка к решению ОПС
        public Lexem ExecuteOPS(LexemString inOPS, bool onlyRead)
        {
            _tempLexem = new Lexem() { Name = "", Type = ElementType.NONE };
            _onlyRead = onlyRead;
            AnalizOPS(inOPS);
            return _tempLexem;
        }

        // Поиск требуемой переменной
        public int FindNumber(string name)
        {
            for (int i = 0; i < _numbers.Count; i++)
            {
                if (_numbers[i].Name == name) return i;
            }

            return -1;
        }

        // Поиск требуемого массива
        public int FindMassive(string name)
        {
            for (int i = 0; i < _massives.Count; i++)
            {
                if (_massives[i].Name == name) return i;
            }

            return -1;
        }

        // state может принимать 9 состояний:
        // 1: ID(var) - ID(var)
        // 2: ID(var) - ID(mas)
        // 3: ID(mas) - ID(var)
        // 4: ID(mas) - ID(mas)
        // 5: ID(var) - NUM
        // 6: ID(mas) - NUM
        // 7: NUM     - ID(var)
        // 8: NUM     - ID(mas)
        // 9: NUM     - NUM
        // где ID - название переменной/массива, NUM - число, var - значение из переменной, mas - значение из ячейки массива 

        #region Арифметические операторы
        // Выполнение операции плюс
        private void Plus(ref Stack<Lexem> tempLexStack, ref Lexem first, ref int indexF, ref Lexem second, ref int indexS, int state)
        {
            switch (state)
            {
                case 1:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_numbers[indexF].Meaning) +
                                    Convert.ToInt32(_numbers[indexS].Meaning)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 2:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_numbers[indexF].Meaning) +
                                        Convert.ToInt32(_massives[indexS].Meaning[second.Index])).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 3:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) +
                                        Convert.ToInt32(_numbers[indexS].Meaning)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 4:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) +
                                            Convert.ToInt32(_massives[indexS].Meaning[second.Index])).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 5:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_numbers[indexF].Meaning) +
                                    Convert.ToInt32(second.Name)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 6:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) +
                                        Convert.ToInt32(second.Name)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 7:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(first.Name) +
                                    Convert.ToInt32(_numbers[indexS].Meaning)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 8:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(first.Name) +
                                        Convert.ToInt32(_massives[indexS].Meaning[second.Index])).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 9:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(first.Name) +
                                    Convert.ToInt32(second.Name)).ToString(),
                        Type = ElementType.INT
                    });
                    break;
            }
        }

        // Выполнение операции минус
        private void Minus(ref Stack<Lexem> tempLexStack, ref Lexem first, ref int indexF, ref Lexem second, ref int indexS, int state)
        {
            switch (state)
            {
                case 1:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_numbers[indexF].Meaning) -
                                    Convert.ToInt32(_numbers[indexS].Meaning)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 2:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_numbers[indexF].Meaning) -
                                        Convert.ToInt32(_massives[indexS].Meaning[second.Index])).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 3:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) -
                                        Convert.ToInt32(_numbers[indexS].Meaning)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 4:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) -
                                            Convert.ToInt32(_massives[indexS].Meaning[second.Index])).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 5:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_numbers[indexF].Meaning) -
                                    Convert.ToInt32(second.Name)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 6:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) -
                                        Convert.ToInt32(second.Name)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 7:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(first.Name) -
                                    Convert.ToInt32(_numbers[indexS].Meaning)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 8:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(first.Name) -
                                        Convert.ToInt32(_massives[indexS].Meaning[second.Index])).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 9:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(first.Name) -
                                    Convert.ToInt32(second.Name)).ToString(),
                        Type = ElementType.INT
                    });
                    break;
            }
        }

        // Выполнение операции умножение
        private void Multiply(ref Stack<Lexem> tempLexStack, ref Lexem first, ref int indexF, ref Lexem second, ref int indexS, int state)
        {
            switch (state)
            {
                case 1:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_numbers[indexF].Meaning) *
                                    Convert.ToInt32(_numbers[indexS].Meaning)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 2:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_numbers[indexF].Meaning) *
                                        Convert.ToInt32(_massives[indexS].Meaning[second.Index])).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 3:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) *
                                        Convert.ToInt32(_numbers[indexS].Meaning)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 4:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) *
                                            Convert.ToInt32(_massives[indexS].Meaning[second.Index])).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 5:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_numbers[indexF].Meaning) *
                                    Convert.ToInt32(second.Name)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 6:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) *
                                        Convert.ToInt32(second.Name)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 7:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(first.Name) *
                                    Convert.ToInt32(_numbers[indexS].Meaning)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 8:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(first.Name) *
                                        Convert.ToInt32(_massives[indexS].Meaning[second.Index])).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 9:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(first.Name) *
                                    Convert.ToInt32(second.Name)).ToString(),
                        Type = ElementType.INT
                    });
                    break;
            }
        }

        // Выполнение операции деление
        private void Divide(ref Stack<Lexem> tempLexStack, ref Lexem first, ref int indexF, ref Lexem second, ref int indexS, int state)
        {
            switch (state)
            {
                case 1:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_numbers[indexF].Meaning) /
                                    Convert.ToInt32(_numbers[indexS].Meaning)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 2:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_numbers[indexF].Meaning) /
                                        Convert.ToInt32(_massives[indexS].Meaning[second.Index])).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 3:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) /
                                        Convert.ToInt32(_numbers[indexS].Meaning)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 4:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) /
                                            Convert.ToInt32(_massives[indexS].Meaning[second.Index])).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 5:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_numbers[indexF].Meaning) /
                                    Convert.ToInt32(second.Name)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 6:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) /
                                        Convert.ToInt32(second.Name)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 7:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(first.Name) /
                                    Convert.ToInt32(_numbers[indexS].Meaning)).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 8:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(first.Name) /
                                        Convert.ToInt32(_massives[indexS].Meaning[second.Index])).ToString(),
                        Type = ElementType.INT
                    });
                    break;

                case 9:
                    tempLexStack.Push(new Lexem()
                    {
                        Name = (Convert.ToInt32(first.Name) /
                                    Convert.ToInt32(second.Name)).ToString(),
                        Type = ElementType.INT
                    });
                    break;
            }
        }
        #endregion

        #region Логические операторы
        // Меньше
        private void Less(ref Stack<Lexem> tempLexStack, ref Lexem first, ref int indexF, ref Lexem second, ref int indexS, int state)
        {
            bool logical = false;
            switch (state)
            {
                case 1:
                    if (Convert.ToInt32(_numbers[indexF].Meaning) < Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 2:
                    if (second.Index >= 0)
                        if (Convert.ToInt32(_numbers[indexF].Meaning) < Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 3:
                    if (first.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) < Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 4:
                    if (first.Index >= 0 && second.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) < Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 5:
                    if (Convert.ToInt32(_numbers[indexF].Meaning) < (Convert.ToInt32(second.Name))) logical = true;
                    break;

                case 6:
                    if (first.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) < Convert.ToInt32(second.Name)) logical = true;
                    break;

                case 7:
                    if (Convert.ToInt32(first.Name) < Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 8:
                    if (second.Index >= 0)
                        if (Convert.ToInt32(first.Name) < Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 9:
                    if (Convert.ToInt32(first.Name) < Convert.ToInt32(second.Name)) logical = true;
                    break;
            }
            tempLexStack.Push(new Lexem()
            {
                Name = logical.ToString(),
                Type = ElementType.BOOL
            });
        }

        // Больше
        private void Greater(ref Stack<Lexem> tempLexStack, ref Lexem first, ref int indexF, ref Lexem second, ref int indexS, int state)
        {
            bool logical = false;
            switch (state)
            {
                case 1:
                    if (Convert.ToInt32(_numbers[indexF].Meaning) > Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 2:
                    if (second.Index >= 0)
                        if (Convert.ToInt32(_numbers[indexF].Meaning) > Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 3:
                    if (first.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) > Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 4:
                    if (first.Index >= 0 && second.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) > Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 5:
                    if (Convert.ToInt32(_numbers[indexF].Meaning) > (Convert.ToInt32(second.Name))) logical = true;
                    break;

                case 6:
                    if (first.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) > Convert.ToInt32(second.Name)) logical = true;
                    break;

                case 7:
                    if (Convert.ToInt32(first.Name) > Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 8:
                    if (second.Index >= 0)
                        if (Convert.ToInt32(first.Name) > Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 9:
                    if (Convert.ToInt32(first.Name) > Convert.ToInt32(second.Name)) logical = true;
                    break;
            }
            tempLexStack.Push(new Lexem()
            {
                Name = logical.ToString(),
                Type = ElementType.BOOL
            });
        }

        // Равно
        private void Equal(ref Stack<Lexem> tempLexStack, ref Lexem first, ref int indexF, ref Lexem second, ref int indexS, int state)
        {
            bool logical = false;
            switch (state)
            {
                case 1:
                    if (Convert.ToInt32(_numbers[indexF].Meaning) == Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 2:
                    if (second.Index >= 0)
                        if (Convert.ToInt32(_numbers[indexF].Meaning) == Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 3:
                    if (first.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) == Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 4:
                    if (first.Index >= 0 && second.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) == Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 5:
                    if (Convert.ToInt32(_numbers[indexF].Meaning) == (Convert.ToInt32(second.Name))) logical = true;
                    break;

                case 6:
                    if (first.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) == Convert.ToInt32(second.Name)) logical = true;
                    break;

                case 7:
                    if (Convert.ToInt32(first.Name) == Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 8:
                    if (second.Index >= 0)
                        if (Convert.ToInt32(first.Name) == Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 9:
                    if (Convert.ToInt32(first.Name) == Convert.ToInt32(second.Name)) logical = true;
                    break;
            }
            tempLexStack.Push(new Lexem()
            {
                Name = logical.ToString(),
                Type = ElementType.BOOL
            });
        }

        // Меньше или равно
        private void LessOrEqual(ref Stack<Lexem> tempLexStack, ref Lexem first, ref int indexF, ref Lexem second, ref int indexS, int state)
        {
            bool logical = false;
            switch (state)
            {
                case 1:
                    if (Convert.ToInt32(_numbers[indexF].Meaning) <= Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 2:
                    if (second.Index >= 0)
                        if (Convert.ToInt32(_numbers[indexF].Meaning) <= Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 3:
                    if (first.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) <= Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 4:
                    if (first.Index >= 0 && second.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) <= Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 5:
                    if (Convert.ToInt32(_numbers[indexF].Meaning) <= (Convert.ToInt32(second.Name))) logical = true;
                    break;

                case 6:
                    if (first.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) <= Convert.ToInt32(second.Name)) logical = true;
                    break;

                case 7:
                    if (Convert.ToInt32(first.Name) <= Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 8:
                    if (second.Index >= 0)
                        if (Convert.ToInt32(first.Name) <= Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 9:
                    if (Convert.ToInt32(first.Name) <= Convert.ToInt32(second.Name)) logical = true;
                    break;
            }
            tempLexStack.Push(new Lexem()
            {
                Name = logical.ToString(),
                Type = ElementType.BOOL
            });
        }

        // Больше или равно
        private void GreaterOrEqual(ref Stack<Lexem> tempLexStack, ref Lexem first, ref int indexF, ref Lexem second, ref int indexS, int state)
        {
            bool logical = false;
            switch (state)
            {
                case 1:
                    if (Convert.ToInt32(_numbers[indexF].Meaning) >= Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 2:
                    if (second.Index >= 0)
                        if (Convert.ToInt32(_numbers[indexF].Meaning) >= Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 3:
                    if (first.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) >= Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 4:
                    if (first.Index >= 0 && second.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) >= Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 5:
                    if (Convert.ToInt32(_numbers[indexF].Meaning) >= (Convert.ToInt32(second.Name))) logical = true;
                    break;

                case 6:
                    if (first.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) >= Convert.ToInt32(second.Name)) logical = true;
                    break;

                case 7:
                    if (Convert.ToInt32(first.Name) >= Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 8:
                    if (second.Index >= 0)
                        if (Convert.ToInt32(first.Name) >= Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 9:
                    if (Convert.ToInt32(first.Name) >= Convert.ToInt32(second.Name)) logical = true;
                    break;
            }
            tempLexStack.Push(new Lexem()
            {
                Name = logical.ToString(),
                Type = ElementType.BOOL
            });
        }

        // Не равно
        private void NotEqual(ref Stack<Lexem> tempLexStack, ref Lexem first, ref int indexF, ref Lexem second, ref int indexS, int state)
        {
            bool logical = false;
            switch (state)
            {
                case 1:
                    if (Convert.ToInt32(_numbers[indexF].Meaning) != Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 2:
                    if (second.Index >= 0)
                        if (Convert.ToInt32(_numbers[indexF].Meaning) != Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 3:
                    if (first.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) != Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 4:
                    if (first.Index >= 0 && second.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) != Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 5:
                    if (Convert.ToInt32(_numbers[indexF].Meaning) != (Convert.ToInt32(second.Name))) logical = true;
                    break;

                case 6:
                    if (first.Index >= 0)
                        if (Convert.ToInt32(_massives[indexF].Meaning[first.Index]) != Convert.ToInt32(second.Name)) logical = true;
                    break;

                case 7:
                    if (Convert.ToInt32(first.Name) != Convert.ToInt32(_numbers[indexS].Meaning)) logical = true;
                    break;

                case 8:
                    if (second.Index >= 0)
                        if (Convert.ToInt32(first.Name) != Convert.ToInt32(_massives[indexS].Meaning[second.Index])) logical = true;
                    break;

                case 9:
                    if (Convert.ToInt32(first.Name) != Convert.ToInt32(second.Name)) logical = true;
                    break;
            }
            tempLexStack.Push(new Lexem()
            {
                Name = logical.ToString(),
                Type = ElementType.BOOL
            });
        }
        #endregion

        // Выполнение заданной операции
        private void Operation(ref Stack<Lexem> tempLexStack, ref Lexem first, ref int indexF, ref Lexem second, ref int indexS, string operation)
        {
            if (first.Type == ElementType.NAME)
            {
                if ((indexF = FindNumber(first.Name)) != -1)
                {
                    if (second.Type == ElementType.NAME)
                    {
                        if ((indexS = FindNumber(second.Name)) != -1)
                        {
                            switch (operation)
                            {
                                case "Plus": Plus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 1); break;
                                case "Minus": Minus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 1); break;
                                case "Multiply": Multiply(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 1); break;
                                case "Divide": Divide(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 1); break;

                                case "Less": Less(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 1); break;
                                case "Greater": Greater(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 1); break;
                                case "Equal": Equal(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 1); break;
                                case "LessOrEqual": LessOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 1); break;
                                case "GreaterOrEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 1); break;
                                case "NotEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 1); break;
                            }
                        }
                        else
                        {
                            if ((indexS = FindMassive(second.Name)) != -1)
                            {
                                switch (operation)
                                {
                                    case "Plus": Plus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 2); break;
                                    case "Minus": Minus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 2); break;
                                    case "Multiply": Multiply(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 2); break;
                                    case "Divide": Divide(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 2); break;

                                    case "Less": Less(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 2); break;
                                    case "Greater": Greater(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 2); break;
                                    case "Equal": Equal(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 2); break;
                                    case "LessOrEqual": LessOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 2); break;
                                    case "GreaterOrEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 2); break;
                                    case "NotEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 2); break;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (second.Type == ElementType.INT)
                        {
                            switch (operation)
                            {
                                case "Plus": Plus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 5); break;
                                case "Minus": Minus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 5); break;
                                case "Multiply": Multiply(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 5); break;
                                case "Divide": Divide(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 5); break;

                                case "Less": Less(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 5); break;
                                case "Greater": Greater(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 5); break;
                                case "Equal": Equal(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 5); break;
                                case "LessOrEqual": LessOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 5); break;
                                case "GreaterOrEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 5); break;
                                case "NotEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 5); break;
                            }
                        }
                    }
                }
                else
                {
                    if ((indexF = FindMassive(first.Name)) != -1)
                    {
                        if (second.Type == ElementType.NAME)
                        {
                            if ((indexS = FindNumber(second.Name)) != -1)
                            {
                                switch (operation)
                                {
                                    case "Plus": Plus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 3); break;
                                    case "Minus": Minus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 3); break;
                                    case "Multiply": Multiply(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 3); break;
                                    case "Divide": Divide(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 3); break;

                                    case "Less": Less(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 3); break;
                                    case "Greater": Greater(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 3); break;
                                    case "Equal": Equal(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 3); break;
                                    case "LessOrEqual": LessOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 3); break;
                                    case "GreaterOrEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 3); break;
                                    case "NotEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 3); break;
                                }
                            }
                            else
                            {
                                if ((indexS = FindMassive(second.Name)) != -1)
                                {
                                    switch (operation)
                                    {
                                        case "Plus": Plus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 4); break;
                                        case "Minus": Minus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 4); break;
                                        case "Multiply": Multiply(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 4); break;
                                        case "Divide": Divide(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 4); break;

                                        case "Less": Less(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 4); break;
                                        case "Greater": Greater(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 4); break;
                                        case "Equal": Equal(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 4); break;
                                        case "LessOrEqual": LessOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 4); break;
                                        case "GreaterOrEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 4); break;
                                        case "NotEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 4); break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (second.Type == ElementType.INT)
                            {
                                switch (operation)
                                {
                                    case "Plus": Plus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 6); break;
                                    case "Minus": Minus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 6); break;
                                    case "Multiply": Multiply(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 6); break;
                                    case "Divide": Divide(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 6); break;

                                    case "Less": Less(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 6); break;
                                    case "Greater": Greater(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 6); break;
                                    case "Equal": Equal(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 6); break;
                                    case "LessOrEqual": LessOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 6); break;
                                    case "GreaterOrEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 6); break;
                                    case "NotEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 6); break;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (first.Type == ElementType.INT)
                {
                    if (second.Type == ElementType.NAME)
                    {
                        if ((indexS = FindNumber(second.Name)) != -1)
                        {
                            switch (operation)
                            {
                                case "Plus": Plus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 7); break;
                                case "Minus": Minus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 7); break;
                                case "Multiply": Multiply(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 7); break;
                                case "Divide": Divide(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 7); break;

                                case "Less": Less(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 7); break;
                                case "Greater": Greater(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 7); break;
                                case "Equal": Equal(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 7); break;
                                case "LessOrEqual": LessOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 7); break;
                                case "GreaterOrEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 7); break;
                                case "NotEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 7); break;
                            }
                        }
                        else
                        {
                            if ((indexS = FindMassive(second.Name)) != -1)
                            {
                                switch (operation)
                                {
                                    case "Plus": Plus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 8); break;
                                    case "Minus": Minus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 8); break;
                                    case "Multiply": Multiply(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 8); break;
                                    case "Divide": Divide(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 8); break;

                                    case "Less": Less(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 8); break;
                                    case "Greater": Greater(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 8); break;
                                    case "Equal": Equal(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 8); break;
                                    case "LessOrEqual": LessOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 8); break;
                                    case "GreaterOrEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 8); break;
                                    case "NotEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 8); break;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (second.Type == ElementType.INT)
                        {
                            switch (operation)
                            {
                                case "Plus": Plus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 9); break;
                                case "Minus": Minus(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 9); break;
                                case "Multiply": Multiply(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 9); break;
                                case "Divide": Divide(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 9); break;

                                case "Less": Less(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 9); break;
                                case "Greater": Greater(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 9); break;
                                case "Equal": Equal(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 9); break;
                                case "LessOrEqual": LessOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 9); break;
                                case "GreaterOrEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 9); break;
                                case "NotEqual": GreaterOrEqual(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, 9); break;
                            }
                        }
                    }
                }
            }
        }

        // Реализациия ОПС
        private void AnalizOPS(LexemString inOPS)
        {
            Lexem first; int indexF = 0;                    // Первый элемент операции
            Lexem second; int indexS = 0;                   // Второй эелемент операции
            Stack<Lexem> tempLexStack = new Stack<Lexem>(); // Временнй стек для хранения терминалов, содержащихся в ОПС

            for (int i = 0; i < inOPS.String.Count; i++)
            {
                switch (inOPS.String[i].Type)
                {
                    #region NAME
                    case ElementType.NAME:
                        tempLexStack.Push(inOPS.String[i]);
                        break;
                    #endregion

                    #region NUM
                    case ElementType.INT:
                        tempLexStack.Push(inOPS.String[i]);
                        break;
                    #endregion

                    #region And &&
                    case ElementType.AND:
                        second = tempLexStack.Pop();
                        first = tempLexStack.Pop();

                        if ((second.Name == first.Name) && (first.Name == "True") &&
                            (first.Type == ElementType.BOOL) && (second.Type == ElementType.BOOL))
                        {
                            tempLexStack.Push(new Lexem()
                            {
                                Name = "True",
                                Type = ElementType.BOOL
                            });
                        }
                        else
                        {
                            tempLexStack.Push(new Lexem()
                            {
                                Name = "False",
                                Type = ElementType.BOOL
                            });
                        }
                        break;
                    #endregion

                    #region Or ||
                    case ElementType.OR:
                        second = tempLexStack.Pop();
                        first = tempLexStack.Pop();

                        if (((second.Name == "True") || (first.Name == "True")) &&
                            (first.Type == ElementType.BOOL) && (second.Type == ElementType.BOOL))
                        {
                            tempLexStack.Push(new Lexem()
                            {
                                Name = "True",
                                Type = ElementType.BOOL
                            });
                        }
                        else
                        {
                            tempLexStack.Push(new Lexem()
                            {
                                Name = "False",
                                Type = ElementType.BOOL
                            });
                        }
                        break;
                    #endregion

                    #region Not !
                    case ElementType.NOT:
                        first = tempLexStack.Pop();

                        if ((first.Name == "True") && (first.Type == ElementType.BOOL))
                        {
                            tempLexStack.Push(new Lexem()
                            {
                                Name = "False",
                                Type = ElementType.BOOL
                            });
                        }
                        else
                        {
                            tempLexStack.Push(new Lexem()
                            {
                                Name = "True",
                                Type = ElementType.BOOL
                            });
                        }
                        break;
                    #endregion

                    #region Less <
                    case ElementType.LESS:
                        second = tempLexStack.Pop();
                        first = tempLexStack.Pop();

                        Operation(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, "Less");
                        break;
                    #endregion

                    #region Greater >
                    case ElementType.GREATER:
                        second = tempLexStack.Pop();
                        first = tempLexStack.Pop();

                        Operation(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, "Greater");
                        break;
                    #endregion

                    #region Equal ==
                    case ElementType.EQ:
                        second = tempLexStack.Pop();
                        first = tempLexStack.Pop();

                        Operation(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, "Equal");
                        break;
                    #endregion

                    #region LessOrEqual >
                    /*case ElementType.LESSOrEqual:
                        second = tempLexStack.Pop();
                        first = tempLexStack.Pop();

                        Operation(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, "LessOrEqual");
                        break;
                    #endregion

                    #region GreaterOrEqual >
                    case ElementType.GREATEROrEqual:
                        second = tempLexStack.Pop();
                        first = tempLexStack.Pop();

                        Operation(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, "GreaterOrEqual");
                        break;*/
                    #endregion

                    #region NotEqual >
                    case ElementType.NEQ:
                        second = tempLexStack.Pop();
                        first = tempLexStack.Pop();

                        Operation(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, "NotEqual");
                        break;
                    #endregion

                    #region ASSIGN :=
                    case ElementType.ASSIGN:
                        if (!_onlyRead)
                        {
                            second = tempLexStack.Pop(); // Присваиваемый элемент
                            first = tempLexStack.Pop();  // Куда присваивается

                            // Место, куда присваивается значение - переменная
                            if ((indexF = FindNumber(first.Name)) != -1)
                            {
                                if (second.Type == ElementType.NAME)
                                {
                                    // Присваиваемый элемент - переменная
                                    if ((indexS = FindNumber(second.Name)) != -1)
                                    {
                                        _numbers[indexF].Meaning = _numbers[indexS].Meaning;
                                    }
                                    else
                                    {
                                        // Присваиваемый элемент - массив
                                        if ((indexS = FindMassive(second.Name)) != -1)
                                        {
                                            _numbers[indexF].Meaning = _massives[indexS].Meaning[second.Index];
                                        }
                                    }
                                }
                                else
                                {
                                    if (second.Type == ElementType.INT)
                                    {
                                        _numbers[indexF].Meaning = Convert.ToInt32(second.Name);
                                    }
                                }

                            }
                            else
                            {
                                // Место, куда присваивается значение - элемент массива
                                if ((indexF = FindMassive(first.Name)) != -1)
                                {
                                    if (second.Type == ElementType.NAME)
                                    {
                                        // Присваиваемый элемент - переменная
                                        if ((indexS = FindNumber(second.Name)) != -1)
                                        {
                                            _massives[indexF].Meaning[first.Index] = _numbers[indexS].Meaning;
                                        }
                                        else
                                        {
                                            // Присваиваемый элемент - массив
                                            if ((indexS = FindMassive(second.Name)) != -1)
                                            {
                                                _massives[indexF].Meaning[first.Index] = _massives[indexS].Meaning[second.Index];
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (second.Type == ElementType.INT)
                                        {
                                            _massives[indexF].Meaning[first.Index] = Convert.ToInt32(second.Name);
                                        }
                                    }

                                }
                            }
                        }
                        break;
                    #endregion

                    #region Plus +
                    case ElementType.PLUS:
                        second = tempLexStack.Pop();
                        first = tempLexStack.Pop();

                        Operation(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, "Plus");
                        break;
                    #endregion

                    #region Minus -
                    case ElementType.MINUS:
                        second = tempLexStack.Pop();
                        first = tempLexStack.Pop();

                        Operation(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, "Minus");
                        break;
                    #endregion

                    #region Multiply *
                    case ElementType.MULT:
                        second = tempLexStack.Pop();
                        first = tempLexStack.Pop();

                        Operation(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, "Multiply");
                        break;
                    #endregion

                    #region Divide /
                    case ElementType.DIVISION:
                        second = tempLexStack.Pop();
                        first = tempLexStack.Pop();

                        #region Проверка на деление на нуль
                        if (second.Type == ElementType.NAME)
                        {
                            if ((indexS = FindNumber(second.Name)) != -1)
                            {
                                if (_numbers[indexS].Meaning == 0)
                                {
                                    Console.WriteLine("Деление на 0");
                                    _tempLexem = new Lexem() { Name = "Error", Type = ElementType.NONE };
                                    return;
                                }
                            }
                            else
                            {
                                if ((indexS = FindMassive(second.Name)) != -1)
                                {
                                    if (_massives[indexS].Meaning[second.Index] == 0)
                                    {
                                        Console.WriteLine("Деление на 0");
                                        _tempLexem = new Lexem() { Name = "Error", Type = ElementType.NONE };
                                        return;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (second.Type == ElementType.INT)
                            {
                                if (second.Name == "0")
                                {
                                    Console.WriteLine("Деление на 0");
                                    _tempLexem = new Lexem() { Name = "Error", Type = ElementType.NONE };
                                    return;
                                }
                            }
                        }
                        #endregion

                        Operation(ref tempLexStack, ref first, ref indexF, ref second, ref indexS, "Divide");
                        break;
                    #endregion

                    #region Undefined
                    case ElementType.NONE:
                        second = tempLexStack.Pop();
                        first = tempLexStack.Pop();

                        if (second.Type == ElementType.INT)
                        {
                            tempLexStack.Push(new Lexem()
                            {
                                Name = first.Name,
                                Type = ElementType.NAME,
                                Index = Convert.ToInt32(second.Name)
                            });
                        }
                        else
                        {
                            if (second.Type == ElementType.NAME)
                            {
                                if ((indexS = FindNumber(second.Name)) != -1)
                                {
                                    tempLexStack.Push(new Lexem()
                                    {
                                        Name = first.Name,
                                        Type = ElementType.NAME,
                                        Index = Convert.ToInt32(_numbers[indexS].Meaning)
                                    });
                                }
                            }
                        }


                        break;
                        #endregion
                }
            }

            // Если что-то осталось после всех операций - перекидываем назад к вызывающему методу
            // В основном нам нужны True и False, остальные не имеют важности
            if (tempLexStack.Count > 0)
            {
                _tempLexem = tempLexStack.Pop();
            }
        }
    }

    class Realizer
    {
        public List<string> Result { get { return _result; } }

        Interpretator _inter;                          // Объект для обработки ОПС

        List<LexemString> _OPS;                        // Готовые ОПС и программы для них
        Stack<Lexem> _afterInter;                      // Результат ОПС и программа для выполнения
        Stack<Lexem> _conditions;                      // Стек открытых условий (if, else, while)
        List<While> _whiles;                           // Коллекция активных циклов While   
        List<string> _result;                          // Все результаты, выводимые интерпретатором через write 
        Stack<bool> _ifBool;                           // Булевы значения прочитанных if

        bool _stopTap = false;                         // Остановка реализации в связи с ошибкой исполнения
        bool _ifTap = true;                            // Условие работы If
        bool _lastIfCondition = true;                  // Состояние последнего закрытого If
        int _countOfOpenIfBracketBeforeFalseIf = 0;    // Колличество открытых скобок перед тем, как мы соскочили на else
        bool _whileTap = false;                        // При считывании While мы ничего не исполняем 
        int _indexTempWhile;                           // Хранение индекса while, который прорабатывается в данный момент 


        // Старт интепретатора
        public Realizer(List<LexemString> OPS, List<Number> numbers, List<Array> massives)
        {
            _OPS = OPS;
            _conditions = new Stack<Lexem>();
            _ifBool = new Stack<bool>();
            _result = new List<string>();
            _whiles = new List<While>();
            _inter = new Interpretator(ref numbers, ref massives);

            for (int i = 0; i < _OPS.Count; i++)
            {
                if (!_stopTap)
                {
                    RealizeProgramm(_OPS[i]);
                }
                else return;

                if (_whiles.Count > 0)
                {
                    if (!_whiles[0].BracketsIsClosed)
                    {
                        if (_whiles[LastOpenedWhile()].Strings.Count != 0)
                        {
                            if (_whiles[LastOpenedWhile()].Active == true
                                 && !_whiles[LastOpenedWhile()].BracketsIsClosed
                                 && _OPS[i].String[0].Type != ElementType.CLOSE_BRACKET
                                 && _conditions.Count >= _whiles[LastOpenedWhile()].Strings[0].String[_whiles[LastOpenedWhile()].Strings[0].String.Count - 1].SimbolPos)
                            {
                                _whiles[LastOpenedWhile()].Strings.Add(_OPS[i]);
                            }
                        }
                        else
                        {
                            if (_whiles[LastOpenedWhile()].Active == true && !_whiles[LastOpenedWhile()].BracketsIsClosed)
                            {
                                _whiles[LastOpenedWhile()].Strings.Add(_OPS[i]);
                            }
                        }
                    }
                }

                // Если считывание while закончилось, то запускаем его до момента остановки активности
                if (_whiles.Count > 0 && _whiles[0].BracketsIsClosed)
                {
                    _whileTap = false;
                    ClearWhilesFromBruckets();
                    RealizeWhile(0);
                    _whiles.Clear();
                }
            }
            return;
        }

        // Получаем результат ОПС и смотрим, есть ли для него подпрограмма
        private void CheckInProgram(LexemString checkingOPS)
        {
            _afterInter = new Stack<Lexem>();                   // Обнуляем прошлый результат
            LexemString tempLexStrForInter = new LexemString()  // Временная строка терминалов для последующей реализации
            { String = new List<Lexem>() };

            // ↓ Кладется в стек
            Lexem tempResultInter = new Lexem();                // Результат, получившийся после работы интерпретатора с ОПС
            Lexem tempProgrammInCommand = new Lexem()           // Программа для обработки полученного результа (может и не быть)
            { Name = "", Type = ElementType.NONE };
            // ↑

            for (int i = 0; i < checkingOPS.String.Count; i++)
            {
                switch (checkingOPS.String[i].Type)
                {
                    // Если находит хоть одну из программ для результата ОПС - запоминает ее
                    case ElementType.WRITE:
                    case ElementType.READ:
                    case ElementType.IF:
                    case ElementType.ELSE:
                    case ElementType.WHILE:
                    case ElementType.IF_WHILE:
                    case ElementType.IN_WHILE:
                    case ElementType.OPEN_FIGURE:
                    case ElementType.CLOSE_FIGURE: tempProgrammInCommand = checkingOPS.String[i]; break;
                    // Иначе просто отправляет для последующей обработки итнерпретатором
                    default: tempLexStrForInter.String.Add(checkingOPS.String[i]); break;
                }
            }
            if (_whileTap || !_ifTap) // Нельзя что-то делать, если условие не выполняется на данном участке или идет считывание для цикла
                tempResultInter = _inter.ExecuteOPS(tempLexStrForInter, true);
            else
                tempResultInter = _inter.ExecuteOPS(tempLexStrForInter, false);

            if (tempResultInter.Name != "Error")
            {
                _afterInter.Push(tempResultInter);
                _afterInter.Push(tempProgrammInCommand);
            }
            else _stopTap = true;
        }

        // Вернуть последний НЕ ЗАКРЫТЫЙ while
        private int LastOpenedWhile()
        {
            for (int i = _whiles.Count - 1; i >= 0; i--)
            {
                if (_whiles[i].BracketsIsClosed == false)
                {
                    return i;
                }
            }
            return -1;
        }

        // Отчистка _whiles от фигурных скобок, обозначающих границы цикла
        private void ClearWhilesFromBruckets()
        {
            for (int i = 0; i < _whiles.Count; i++)
            {
                _whiles[i].Strings.RemoveAt(1);
                _whiles[i].Strings.RemoveAt(_whiles[i].Strings.Count - 1);
            }
        }

        // Запуск сохраненных _whiles
        private void RealizeWhile(int neededWhileIndex)
        {
            While tempWhile = new While();

            for (int i = 0; i < _whiles.Count; i++)
            {
                if (_whiles[i].Index == neededWhileIndex)
                {
                    tempWhile = _whiles[i];
                    break;
                }
            }

            tempWhile.Active = true;
            do
            {
                // Временное хранение индекса while, который используется именно сейчас
                _indexTempWhile = tempWhile.Index;
                for (int k = 0; k < tempWhile.Strings.Count; k++)
                {
                    if (tempWhile.Active)
                        RealizeProgramm(tempWhile.Strings[k]);
                }
            }
            while (tempWhile.Active);
        }

        // Реализациия программы
        private void RealizeProgramm(LexemString inOPS)
        {
            CheckInProgram(inOPS);
            if (_stopTap) return;

            Lexem result; int indexF = 0;
            Lexem program;
            program = _afterInter.Pop();
            result = _afterInter.Pop();

            if (program.Name != "")
            {
                // Если у нас if - false и не идет чтение ОПС в цикл
                if (!_ifTap && !_whileTap)
                {
                    if (program.Type == ElementType.OPEN_FIGURE)
                    {
                        _conditions.Push(new Lexem() { Name = "LeftFigurBracket", Type = ElementType.OPEN_FIGURE });
                    }
                    else
                    {
                        if (program.Type == ElementType.CLOSE_FIGURE)
                        {
                            _conditions.Pop();
                        }
                    }

                    if (_countOfOpenIfBracketBeforeFalseIf == _conditions.Count)
                    {
                        _ifTap = true;
                        if (_ifBool.Count > 0) _lastIfCondition = _ifBool.Pop();
                    }
                }
                else // Обычное выполнение или чтение строк в цикл
                {
                    switch (program.Type)
                    {
                        #region Write
                        case ElementType.WRITE:
                            if (!_whileTap)
                            {
                                if (result.Type == ElementType.NAME)
                                {
                                    if ((indexF = _inter.FindNumber(result.Name)) != -1)
                                    {
                                        _result.Add(_inter.Numbers[indexF].Meaning.ToString());
                                    }
                                    else
                                    {
                                        if ((indexF = _inter.FindMassive(result.Name)) != -1)
                                        {
                                            _result.Add(_inter.Massives[indexF].Meaning[result.Index].ToString());
                                        }
                                    }
                                }
                                else
                                {
                                    if (result.Type == ElementType.INT)
                                    {
                                        _result.Add(result.Name);
                                    }
                                }
                            }
                            break;
                        #endregion

                        #region Read
                        case ElementType.READ:
                            if (!_whileTap)
                            {
                                int rd = Console.Read();
                                if (result.Type == ElementType.NAME)
                                {
                                    if ((indexF = _inter.FindNumber(result.Name)) != -1)
                                    {
                                        _inter.Numbers[indexF].Meaning = Convert.ToInt32(rd);
                                    }
                                    else
                                    {
                                        if ((indexF = _inter.FindMassive(result.Name)) != -1)
                                        {
                                            _inter.Massives[indexF].Meaning[result.Index] = Convert.ToInt32(rd);
                                        }
                                    }
                                }
                            }
                            break;
                        #endregion

                        #region If
                        case ElementType.IF:
                            if (!_whileTap)
                            {
                                if (result.Name == "True" && result.Type == ElementType.BOOL)
                                {
                                    _conditions.Push(new Lexem() { Name = "if", Type = ElementType.IF });
                                    _ifBool.Push(true);
                                    _countOfOpenIfBracketBeforeFalseIf++;

                                    _ifTap = true;
                                }
                                else
                                {
                                    if (result.Name == "False" && result.Type == ElementType.BOOL)
                                    {
                                        _ifBool.Push(false);

                                        _ifTap = false;
                                    }
                                }
                            }
                            break;
                        #endregion

                        #region Else
                        case ElementType.ELSE:
                            if (!_whileTap)
                            {
                                if (_lastIfCondition)
                                {
                                    // если if был True -  не выполняем else
                                    _ifTap = false;
                                }
                                else
                                {
                                    // если if был False - выполняем и записываем его в _confition
                                    _conditions.Push(new Lexem() { Name = "else", Type = ElementType.ELSE });
                                    _countOfOpenIfBracketBeforeFalseIf++;

                                    _ifTap = true;
                                }
                            }
                            break;
                        #endregion

                        #region While
                        case ElementType.WHILE:
                            LexemString tempString = inOPS;
                            tempString.String[tempString.String.Count - 1].Name = "IfWhile";
                            tempString.String[tempString.String.Count - 1].Type = ElementType.IF_WHILE;
                            tempString.String[tempString.String.Count - 1].SimbolPos = _conditions.Count;
                            // Вложенный
                            if (_whiles.Count > 0)
                            {
                                // Добавляем ссылку в последний открытый цикл на будущий while
                                _whiles[LastOpenedWhile()].Strings.Add(new LexemString()
                                {
                                    String = new List<Lexem>() {
                                    new Lexem() { Name = "InWhile", Type = ElementType.IN_WHILE, Index = _whiles.Count} }
                                });
                            }
                            _whiles.Add(new While() { Strings = new List<LexemString>(), BracketsIsClosed = false, Active = true, Index = _whiles.Count });
                            _whileTap = true;
                            break;
                        #endregion

                        #region IfWhile
                        case ElementType.IF_WHILE:
                            if (result.Name == "False" && result.Type == ElementType.BOOL)
                            {
                                _whiles[_indexTempWhile].Active = false;
                            }
                            break;
                        #endregion

                        #region InWhile
                        case ElementType.IN_WHILE:
                            RealizeWhile(program.Index);
                            break;
                        #endregion

                        #region LeftFigurBracket
                        case ElementType.OPEN_FIGURE:
                            if (_whileTap)
                            {
                                _conditions.Push(new Lexem() { Name = "LeftFigurBracket", Type = ElementType.OPEN_FIGURE });
                                _countOfOpenIfBracketBeforeFalseIf++;
                            }
                            break;
                        #endregion

                        #region RightFigurBracket
                        case ElementType.CLOSE_FIGURE:
                            _conditions.Pop();
                            while (_conditions.Count != _ifBool.Count && !_whileTap)
                            {
                                _lastIfCondition = _ifBool.Pop();
                            }
                            _countOfOpenIfBracketBeforeFalseIf--;
                            if (_whiles.Count > 0 && _whileTap)
                            {
                                if (_conditions.Count > _whiles[LastOpenedWhile()].Strings[0].String[_whiles[LastOpenedWhile()].Strings[0].String.Count - 1].SimbolPos)
                                {
                                    _whiles[LastOpenedWhile()].Strings.Add(new LexemString() { String = inOPS.String });
                                }
                                else
                                {
                                    if (_conditions.Count == _whiles[LastOpenedWhile()].Strings[0].String[_whiles[LastOpenedWhile()].Strings[0].String.Count - 1].SimbolPos)
                                    {
                                        _whiles[LastOpenedWhile()].Strings.Add(new LexemString() { String = inOPS.String });
                                        _whiles[LastOpenedWhile()].BracketsIsClosed = true;
                                    }
                                }
                            }
                            break;
                            #endregion
                    }
                }
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            SyntaxAnalizator syntax;
            Realizer realiz;
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
            /*Console.WriteLine("Массивы:\r\n");
            for (int i = 0; i < lexem.arrays.Count; i++)
            {
                Console.WriteLine(lexem.arrays[i].Name + " - ");
                for (int j = 0; j < lexem.arrays[i].Meaning.Length; j++)
                {
                    Console.WriteLine(lexem.arrays[i].Meaning[j] + " ");
                }
            }*/

            //Результат работы синтаксического анализатора
            syntax = new SyntaxAnalizator(lexem.Lexems);
            Console.WriteLine("Созданные ОПС на основе исходных лексем:\r\n");
            for (int i = 0; i < syntax.OPS.Count; i++)
            {
                for (int j = 0; j < syntax.OPS[i].String.Count; j++)
                {
                    Console.Write(syntax.OPS[i].String[j].Name + " ");
                }
                Console.WriteLine("\r\n");
            }

            //Результат работы
            realiz = new Realizer(syntax.OPS, lexem.Numbers, lexem.Arrays);
            for (int i = 0; i < realiz.Result.Count; i++)
            {
                Console.Write(realiz.Result[i] + "\r\n");
            }

            Console.ReadLine();
        }
    }
}