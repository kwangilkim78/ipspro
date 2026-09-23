namespace IPS_PROBACKOFFICE.Helpers;

internal static class BokSequenceValidation
{
    public static (bool Success, string Message) Check(IEnumerable<string?> values)
    {
        var rows = values.ToList();
        if (rows.Count == 0)
            return (false, "조회 결과가 없습니다. 검증할 거래가 없습니다.");

        var numbers = new List<long>();
        foreach (var value in rows)
        {
            if (!long.TryParse(value, out long number) || number <= 0)
                return (false, "검증실패: 거래 번호에 빈 값 또는 올바르지 않은 값이 있습니다.");
            numbers.Add(number);
        }

        numbers.Sort();
        long last = numbers[^1];
        bool continuous = numbers.Select((number, index) => number == index + 1L).All(valid => valid);
        if (continuous)
            return (true, $"검증완료: 마지막 시퀀스 {last:N0} = 조회 건수 {rows.Count:N0}건\n1~{last:N0}번 누락·중복 없음");

        // Describe missing ranges without allocating a list up to an invalid, very large ID.
        var missing = new List<string>();
        long previous = 0;
        foreach (long number in numbers.Distinct())
        {
            if (number - previous > 1)
                missing.Add(number - previous == 2 ? $"{previous + 1}" : $"{previous + 1}~{number - 1}");
            previous = number;
        }
        var duplicates = numbers.GroupBy(number => number).Where(group => group.Count() > 1)
            .Select(group => group.Key.ToString()).ToList();
        string message = $"검증실패: 마지막 시퀀스 {last:N0}, 조회 건수 {rows.Count:N0}건";
        if (missing.Count > 0)
            message += "\n누락 번호: " + string.Join(", ", missing.Take(20)) + (missing.Count > 20 ? " …" : "");
        if (duplicates.Count > 0)
            message += "\n중복 번호: " + string.Join(", ", duplicates.Take(20)) + (duplicates.Count > 20 ? " …" : "");
        return (false, message);
    }
}
