﻿using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension
{
    public class Challenge
    {
        public Lesson Lesson { get; set; }
        public IReadOnlyList<EditableCode> Contents { get; private set; }
        public bool Passed { get; private set; } = false;
        public bool Revealed { get; private set; } = false;
        public List<Challenge> Dependencies { get; private set; } = new List<Challenge>();
        public List<Challenge> Dependents { get; private set; } = new List<Challenge>();
        public Func<ChallengeContext, Task> OnCodeSubmittedHandler { get; private set; }

        private List<Rule> _rules = new();

        private List<Action<Challenge>> _onRevealListeners = new List<Action<Challenge>>();
        private List<Action<Challenge>> _onFocusListeners = new List<Action<Challenge>>();

        private ChallengeContext context;

        public Challenge(IReadOnlyList<EditableCode> content, Lesson lesson = null)
        {
            Contents = content;
            Lesson = lesson;
            context = new ChallengeContext(lesson);
        }

        public void AddDependency(Challenge challenge)
        {
            Dependencies.Add(challenge);
        }

        public void AddDependent(Challenge challenge)
        {
            Dependents.Add(challenge);
        }

        public void AddOnRevealListener(Action<Challenge> listener)
        {
            _onRevealListeners.Add(listener);
        }

        public void AddOnFocusListener(Action<Challenge> listener)
        {
            _onFocusListeners.Add(listener);
        }

        public void Pass()
        {
            Passed = true;
            foreach (var dependent in Dependents)
            {
                if (dependent.Revealed || dependent.CanReveal())
                {
                    dependent.Focus();
                }
            }
        }
        public void Focus()
        {
            foreach (var listener in _onFocusListeners)
            {
                listener(this);
            }
            Reveal();
        }
        public bool CanReveal()
        {
            return Dependencies
                .Select(dependency => dependency.Passed)
                .All(passed => passed);
        }

        public void Reveal()
        {
            if (!Revealed)
            {
                foreach (var listener in _onRevealListeners)
                {
                    listener(this);
                }
                Revealed = true; 
            }
        }

        public void ClearDependencyRelationships()
        {
            Dependencies.Clear();
            Dependents.Clear();
        }

        public async Task InvokeOnEvaluationComplete()
        {
            await OnCodeSubmittedHandler(context);
        }

        public Evaluation EvaluateByDefault(RuleContext result)
        {
            // todo: result unused
            // prob remove this arg because 
            // we'll use challenge info in this object to construct rulecontext
            // to pass them into rule.Evaluate()
            var evaluation = new Evaluation();

            var listOfRulePassOrFailOutcomes = new List<bool>();
            foreach (var rule in _rules)
            {
                var ruleContext = new RuleContext();
                rule.Evaluate(ruleContext);
                listOfRulePassOrFailOutcomes.Add(ruleContext.Passed);
            }

            if (listOfRulePassOrFailOutcomes.Contains(false))
            {
                evaluation.SetOutcome(Outcome.Failure);
            }
            else
            {
                evaluation.SetOutcome(Outcome.Success);
            }
            return evaluation;
        }

        public void AddRuleAsync(Func<RuleContext, Task> action)
        {
            AddRule(new Rule(action));
        }

        public void AddRule(Action<RuleContext> action)
        {
            AddRuleAsync((context) =>
            {
                action(context);
                return Task.CompletedTask;
            });
        }

        public void OnCodeSubmittedAsync(Func<ChallengeContext, Task> action)
        {
            OnCodeSubmittedHandler = action;
        }

        public void OnCodeSubmitted(Action<ChallengeContext> action)
        {
            OnCodeSubmittedAsync((context) =>
            {
                action(context);
                return Task.CompletedTask;
            });
        }

        private void AddRule(Rule rule)
        {
            _rules.Add(rule);
        }
    }
}