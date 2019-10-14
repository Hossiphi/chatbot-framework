﻿using Framework.Luis;
using Framework.Misc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Framework.QuestionAnalyzer
{
    /// <summary>
    /// Examine input with regard to questions.
    /// </summary>
    public sealed class Analyzer
    {
        private readonly LuisRecognizer _classifier;
        private readonly double _threshold;
        private Task<RecognizerResult> _classification;
        private string _response;

        private static readonly Dictionary<Language, string> Configs = new Dictionary<Language, string> {
            { Language.English, "en" },
            { Language.Deutsch, "de" }
        };

        /// <summary>
        /// Creates the analyzer.
        /// </summary>
        /// <param name="config">The configuration of the bot</param>
        /// <param name="lang">Defines the language.</param>
        /// <param name="threshold">The default threshold for Luis classification.</param>
        /// <exception cref="ArgumentException">unsupported language.</exception>
        public Analyzer(IConfiguration config, Language lang, double threshold = 0.7)
        {
            if (!Configs.ContainsKey(lang))
                throw new ArgumentException("Your Language is not supported.");
            var map = config.GetSection("QuestionAnalyzer").GetSection(Configs[lang]).Get<Dictionary<string, object>>();
            LuisServiceDefinition lsd = JsonConvert.DeserializeObject<LuisServiceDefinition>(JsonConvert.SerializeObject(map));
            _classifier = new LuisRecognizer(lsd.GetLuisService(), lsd.GetPredictOpts());
            _threshold = threshold;
        }

        /// <summary>
        /// Recognize a new input.
        /// </summary>
        /// <param name="turnContext">The bot context with input.</param>
        public void Recognize(ITurnContext turnContext)
        {
            _response = turnContext.Activity.Text;
            _classification = _classifier.RecognizeAsync(turnContext, default(CancellationToken));
        }

        /// <summary>
        /// Retrieve the found question types.
        /// </summary>
        /// <returns>The found question types with their probability.</returns>
        public async Task<List<Tuple<QuestionType, double>>> GetQuestionTypes()
        {
            var classification = await _classification;
            List<Tuple<QuestionType, double>> questionTypesOverThreshold = new List<Tuple<QuestionType, double>>();

            foreach (string qt in classification.Intents.Keys)
            {
                double? score = classification.Intents[qt].Score;
                if ((score != null) && (score >= _threshold))
                    questionTypesOverThreshold.Add(Enum.Parse<QuestionType>(qt), (double)score);
            }
            return questionTypesOverThreshold;
        }

        /// <summary>
        /// Builds a wrapper to execute actions according to the question type.
        /// </summary>
        /// <param name="none">Mandatory parameter, which will be executed if no question had an probility over threshold or no handler is definied for the classified question.</param>
        /// <param name="how">The handler for "how" questions.</param>
        /// <param name="howMany">The handler for "how many" questions.</param>
        /// <param name="what">The handler for "what" questions.</param>
        /// <param name="where">The handler for "where" questions.</param>
        /// <param name="who">The handler for "who" questions.</param>
        /// <param name="why">The handler for "why" questions.</param>
        /// <param name="when">The handler for "when" questions.</param>
        /// <returns></returns>
        public async Task HandleQuestion(Action none, Action how = null, Action howMany = null, Action what = null, Action where = null, Action who = null, Action why = null, Action when = null)
        {
            if (none == null)
                throw new ArgumentNullException(nameof(none));

            var questionTypes = await GetQuestionTypes();

            QuestionType types = GetMaximialIntents(questionTypes);
            Dictionary<QuestionType, Action> handlers = new Dictionary<QuestionType, Action> {
                { QuestionType.How, how },
                { QuestionType.HowMany, howMany },
                { QuestionType.What, what },
                { QuestionType.Where, where },
                { QuestionType.Who, who },
                { QuestionType.Why, why },
                { QuestionType.When, when },
            };

            if (!handlers.TryGetValue(types, out Action handler))
                handler = none;
            if (handler == null)
                handler = none;

            handler();
        }

        private static QuestionType GetMaximialIntents(List<Tuple<QuestionType, double>> _this)
        {
            if (_this.Count == 0)
                return QuestionType.None;


            QuestionType maxKey = _this[0].Item1;
            double maxValue = _this[0].Item2;


            for (int i = 0; i < _this.Count; i++)
            {
                if (_this[i].Item2 > maxValue)
                {
                    maxKey = _this[i].Item1;
                    maxValue = _this[i].Item2;
                }
            }

            return maxKey;

        }
    }
}