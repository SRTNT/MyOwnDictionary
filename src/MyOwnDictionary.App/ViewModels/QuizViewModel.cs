using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyOwnDictionary.Core;

namespace MyOwnDictionary.App.ViewModels;

public partial class QuizViewModel : ObservableObject
{
    public const int MinimumQuestions = 5;
    public const int MaximumQuestions = 30;

    private readonly Language _language;
    private readonly QuizRepository _quiz;
    private readonly List<QuizWord> _queue = [];
    private int _index = -1;
    private QuizWord? _current;
    private bool _sessionTyped;

    public QuizViewModel(Language language, QuizRepository quiz)
    {
        _language = language;
        _quiz = quiz;
    }

    public ObservableCollection<QuizChoice> Choices { get; } = [];
    public ObservableCollection<string> NewlyMastered { get; } = [];

    public FlowDirection PromptFlow => _language == Language.Fa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public FlowDirection AnswerFlow => _language == Language.Fa ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
    public bool ShowChoices => IsQuestion && !_sessionTyped;
    public bool ShowTyped => IsQuestion && _sessionTyped;
    public bool ShowWrong => HasFeedback && !LastCorrect;
    public bool HasNewlyMastered => NewlyMastered.Count > 0;
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
    public bool PracticeLearningSelected => !ReviewMastered;
    public bool PracticeChoiceSelected => !TypeAnswers;
    public string SummaryLabel => $"{CorrectTotal} correct, {WrongTotal} wrong.";
    public string MasteredListText => string.Join(Environment.NewLine, NewlyMastered);

    [ObservableProperty] private int _learningCount;
    [ObservableProperty] private int _masteredCount;
    [ObservableProperty] private bool _reviewMastered;
    [ObservableProperty] private bool _typeAnswers;
    [ObservableProperty] private decimal? _questionCount = 10;
    [ObservableProperty] private bool _isStart = true;
    [ObservableProperty] private bool _isQuestion;
    [ObservableProperty] private bool _isSummary;
    [ObservableProperty] private bool _canStart;
    [ObservableProperty] private bool _canAnswer;
    [ObservableProperty] private string _prompt = "";
    [ObservableProperty] private string _typedAnswer = "";
    [ObservableProperty] private string _feedback = "";
    [ObservableProperty] private bool _hasFeedback;
    [ObservableProperty] private bool _lastCorrect;
    [ObservableProperty] private string _wordProgress = "";
    [ObservableProperty] private string _sessionLabel = "";
    [ObservableProperty] private int _answered;
    [ObservableProperty] private int _sessionTotal;
    [ObservableProperty] private int _correctTotal;
    [ObservableProperty] private int _wrongTotal;
    [ObservableProperty] private string _note = "";

    public string CountsLabel => $"{LearningCount} still learning · {MasteredCount} mastered";

    public async Task RefreshAsync()
    {
        var counts = await _quiz.CountAsync(_language);
        LearningCount = counts.Learning;
        MasteredCount = counts.Mastered;
        UpdateAvailability();
    }

    partial void OnReviewMasteredChanged(bool value)
    {
        OnPropertyChanged(nameof(PracticeLearningSelected));
        UpdateAvailability();
    }

    partial void OnTypeAnswersChanged(bool value) => OnPropertyChanged(nameof(PracticeChoiceSelected));

    partial void OnNoteChanged(string value) => OnPropertyChanged(nameof(HasNote));

    partial void OnCorrectTotalChanged(int value) => OnPropertyChanged(nameof(SummaryLabel));

    partial void OnWrongTotalChanged(int value) => OnPropertyChanged(nameof(SummaryLabel));

    partial void OnQuestionCountChanged(decimal? value)
    {
        if (value is null)
            return;

        var clamped = Math.Clamp((int)value.Value, MinimumQuestions, MaximumQuestions);
        if (clamped != (int)value.Value)
            QuestionCount = clamped;
    }

    partial void OnTypedAnswerChanged(string value) => CheckCommand.NotifyCanExecuteChanged();

    partial void OnCanAnswerChanged(bool value)
    {
        CheckCommand.NotifyCanExecuteChanged();
        foreach (var choice in Choices)
            choice.Refresh();
    }

    partial void OnIsQuestionChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowChoices));
        OnPropertyChanged(nameof(ShowTyped));
    }

    partial void OnHasFeedbackChanged(bool value) => OnPropertyChanged(nameof(ShowWrong));

    partial void OnLastCorrectChanged(bool value) => OnPropertyChanged(nameof(ShowWrong));

    partial void OnLearningCountChanged(int value) => OnPropertyChanged(nameof(CountsLabel));

    partial void OnMasteredCountChanged(int value) => OnPropertyChanged(nameof(CountsLabel));

    [RelayCommand]
    private void PracticeLearning() => ReviewMastered = false;

    [RelayCommand]
    private void PracticeMastered() => ReviewMastered = true;

    [RelayCommand]
    private void UseChoices() => TypeAnswers = false;

    [RelayCommand]
    private void UseTyping() => TypeAnswers = true;

    [RelayCommand(CanExecute = nameof(CanStartQuiz))]
    private async Task StartAsync()
    {
        await RefreshAsync();
        if (!CanStart)
            return;

        var requested = (int)(QuestionCount ?? 10);
        var pool = ReviewMastered ? QuizPool.Mastered : QuizPool.Learning;
        var words = await _quiz.ListAsync(_language, pool);
        _queue.Clear();
        _queue.AddRange(words.Take(requested));
        _sessionTyped = TypeAnswers;
        _index = -1;
        CorrectTotal = 0;
        WrongTotal = 0;
        Answered = 0;
        SessionTotal = _queue.Count;
        NewlyMastered.Clear();
        OnPropertyChanged(nameof(HasNewlyMastered));
        OnPropertyChanged(nameof(MasteredListText));
        Note = _queue.Count < requested
            ? $"This round has {_queue.Count} words."
            : "";
        IsStart = false;
        IsSummary = false;
        await ShowNextAsync();
    }

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private Task CheckAsync() => AnswerAsync(TypedAnswer);

    private bool CanCheck() => CanAnswer && _sessionTyped && !string.IsNullOrWhiteSpace(TypedAnswer);

    [RelayCommand]
    private async Task NextAsync()
    {
        if (!HasFeedback)
            return;

        await ShowNextAsync();
    }

    [RelayCommand]
    private async Task BackToStartAsync()
    {
        IsQuestion = false;
        IsSummary = false;
        IsStart = true;
        HasFeedback = false;
        await RefreshAsync();
    }

    public Task ChooseAsync(string choice) => AnswerAsync(choice);

    private async Task ShowNextAsync()
    {
        _index++;
        if (_index >= _queue.Count)
        {
            IsQuestion = false;
            IsSummary = true;
            CanAnswer = false;
            await RefreshAsync();
            return;
        }

        _current = _queue[_index];
        Prompt = _current.Prompt;
        TypedAnswer = "";
        Feedback = "";
        HasFeedback = false;
        LastCorrect = false;
        WordProgress = $"{Math.Min(_current.CorrectCount, QuizRepository.MasteryThreshold)} / {QuizRepository.MasteryThreshold}";
        SessionLabel = $"Question {_index + 1} of {SessionTotal}";
        Choices.Clear();
        CanAnswer = true;
        IsQuestion = true;
        OnPropertyChanged(nameof(ShowChoices));
        OnPropertyChanged(nameof(ShowTyped));

        if (_sessionTyped)
            return;

        var options = await _quiz.BuildChoicesAsync(_language, _current.Translation);
        if (!CanAnswer || _current != _queue[_index])
            return;

        foreach (var option in options)
            Choices.Add(new QuizChoice(this, option, AnswerFlow));
    }

    private async Task AnswerAsync(string given)
    {
        if (!CanAnswer || _current is null)
            return;

        CanAnswer = false;
        var correct = QuizRepository.AnswersMatch(_current.Translation, given, LanguageDetector.Opposite(_language));
        var score = await _quiz.RecordAsync(_language, _current.NormalizedText, correct);
        if (correct)
        {
            CorrectTotal++;
            Feedback = "Correct";
            LastCorrect = true;
        }
        else
        {
            WrongTotal++;
            Feedback = "Answer: " + _current.Translation;
            LastCorrect = false;
        }

        WordProgress = $"{Math.Min(score.CorrectCount, QuizRepository.MasteryThreshold)} / {QuizRepository.MasteryThreshold}";
        if (score.JustMastered)
        {
            NewlyMastered.Add(_current.Prompt + "  →  " + _current.Translation);
            OnPropertyChanged(nameof(HasNewlyMastered));
            OnPropertyChanged(nameof(MasteredListText));
        }

        Answered = _index + 1;
        HasFeedback = true;
    }

    private bool CanStartQuiz() => CanStart;

    private void UpdateAvailability()
    {
        var available = ReviewMastered ? MasteredCount : LearningCount;
        CanStart = available > 0;
        if (available == 0)
        {
            Note = ReviewMastered
                ? "No mastered words yet. A word is mastered after 10 correct answers."
                : "Translate a few words first. The quiz uses words from this history.";
        }
        else if (IsStart)
        {
            Note = "";
        }

        StartCommand.NotifyCanExecuteChanged();
    }
}

public sealed class QuizChoice
{
    private readonly QuizViewModel _owner;

    public QuizChoice(QuizViewModel owner, string text, FlowDirection flow)
    {
        _owner = owner;
        Text = text;
        Flow = flow;
        SelectCommand = new AsyncRelayCommand(() => owner.ChooseAsync(text), () => owner.CanAnswer);
    }

    public string Text { get; }
    public FlowDirection Flow { get; }
    public IAsyncRelayCommand SelectCommand { get; }

    public void Refresh() => SelectCommand.NotifyCanExecuteChanged();
}
