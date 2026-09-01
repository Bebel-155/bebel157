using System;

static class TestAssert
{
    public static void Equal<T>(T expected, T actual, string label)
    {
        if (!object.Equals(expected, actual))
            throw new Exception(label + " expected=" + expected + " actual=" + actual);
    }

    public static void True(bool value, string label)
    {
        if (!value) throw new Exception(label);
    }

    public static void Contains(string expected, string actual, string label)
    {
        if ((actual ?? "").IndexOf(expected ?? "", StringComparison.OrdinalIgnoreCase) < 0)
            throw new Exception(label + " expected fragment=" + expected + " actual=" + actual);
    }
}
