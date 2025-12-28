using Terminal.Gui;

namespace DHM.Views;

public class LoadingView
{
    public static void ShowWhile(string message, Action work)
    {
        var dialog = new Dialog("", 50, 7)
        {
            Border = { BorderStyle = BorderStyle.Rounded }
        };

        var label = new Label(message)
        {
            X = Pos.Center(),
            Y = 1
        };

        var progressBar = new ProgressBar()
        {
            X = 2,
            Y = 3,
            Width = Dim.Fill() - 4,
            Height = 1
        };

        dialog.Add(label, progressBar);

        var completed = false;

        // 프로그레스 바 애니메이션
        _ = Task.Run(async () =>
        {
            var progress = 0f;
            while (!completed)
            {
                progress += 0.05f;
                if (progress > 1) progress = 0;

                Application.MainLoop?.Invoke(() =>
                {
                    progressBar.Fraction = progress;
                });

                await Task.Delay(50);
            }
        });

        // 실제 작업 실행
        _ = Task.Run(() =>
        {
            work();
            completed = true;

            Application.MainLoop?.Invoke(() =>
            {
                Application.RequestStop();
            });
        });

        Application.Run(dialog);
    }

    public static async Task<T> ShowWhileAsync<T>(string message, Func<T> work)
    {
        T result = default!;

        var dialog = new Dialog("", 50, 7)
        {
            Border = { BorderStyle = BorderStyle.Rounded }
        };

        var label = new Label(message)
        {
            X = Pos.Center(),
            Y = 1
        };

        var progressBar = new ProgressBar()
        {
            X = 2,
            Y = 3,
            Width = Dim.Fill() - 4,
            Height = 1
        };

        dialog.Add(label, progressBar);

        var completed = false;

        // 프로그레스 바 애니메이션
        _ = Task.Run(async () =>
        {
            var progress = 0f;
            while (!completed)
            {
                progress += 0.05f;
                if (progress > 1) progress = 0;

                Application.MainLoop?.Invoke(() =>
                {
                    progressBar.Fraction = progress;
                });

                await Task.Delay(50);
            }
        });

        // 실제 작업 실행
        _ = Task.Run(() =>
        {
            result = work();
            completed = true;

            Application.MainLoop?.Invoke(() =>
            {
                Application.RequestStop();
            });
        });

        Application.Run(dialog);
        return result;
    }
}
