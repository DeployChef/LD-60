using System;
using System.Collections.Generic;
using LD60.Core.Bus;
using LD60.Gameplay.Context;
using LD60.Gameplay.Story;
using TMPro;
using UnityEngine;
using VContainer;

namespace LD60.UI
{
    public sealed class DeskMoneyView : MonoBehaviour
    {
        [SerializeField] private GameObject[] bills;
        [SerializeField] private TMP_Text amount;
        [Tooltip("Coins needed before the next bill appears. The number above the pile is the real balance.")]
        [SerializeField] private int coinsPerBill = 40;

        private StoryContext _context;
        private IDisposable _day;
        private IDisposable _money;

        [Inject]
        public void Construct(IMessageBus bus, StoryContext context)
        {
            _context = context;
            _day?.Dispose();
            _money?.Dispose();
            _day = bus.Subscribe<DayStarted>(_ => Refresh());
            _money = bus.Subscribe<MoneyChanged>(_ => Refresh());
            Refresh();
        }

        private void Awake()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            _day?.Dispose();
            _money?.Dispose();
        }

        private void Refresh()
        {
            EnsureRefs();
            var balance = _context != null ? _context.Money : 0;
            var shown = BillsFor(balance);
            if (bills != null)
            {
                for (var i = 0; i < bills.Length; i++)
                {
                    if (bills[i] != null)
                        bills[i].SetActive(i < shown);
                }
            }

            if (amount != null)
                amount.text = balance.ToString();
        }

        private int BillsFor(int balance)
        {
            if (balance <= 0 || bills == null || bills.Length == 0)
                return 0;

            var step = Mathf.Max(1, coinsPerBill);
            return Mathf.Clamp(Mathf.CeilToInt(balance / (float)step), 1, bills.Length);
        }

        private void EnsureRefs()
        {
            if (bills == null || bills.Length == 0)
                bills = CollectBills();

            if (amount == null)
            {
                var label = transform.Find("Amount");
                if (label != null)
                    amount = label.GetComponent<TMP_Text>();
            }
        }

        private GameObject[] CollectBills()
        {
            var found = new List<GameObject>();
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("Money_", StringComparison.Ordinal))
                    found.Add(child.gameObject);
            }

            found.Sort((a, b) => IndexOf(a.name).CompareTo(IndexOf(b.name)));
            return found.ToArray();
        }

        private static int IndexOf(string name)
        {
            var underscore = name.LastIndexOf('_');
            if (underscore < 0 || underscore >= name.Length - 1)
                return int.MaxValue;

            return int.TryParse(name.Substring(underscore + 1), out var index) ? index : int.MaxValue;
        }
    }
}
