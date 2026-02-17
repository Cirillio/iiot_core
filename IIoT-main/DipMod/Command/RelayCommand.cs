using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DipMod.Command
{
    /// <summary>
    /// Реализация паттерна Command для MVVM.
    /// Используется для связывания действий (Action) с элементами UI (например, кнопками).
    /// </summary>
    internal class RelayCommand : ICommand
    {
        private Action<object> execute; // Действие, которое будет выполнено
        private Func<object, bool> canExecute; // Функция проверки возможности выполнения действия

        /// <summary>
        /// Событие, вызываемое при изменении условий выполнения команды.
        /// Использует CommandManager.RequerySuggested для автоматического обновления состояния UI.
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return this.canExecute == null || this.canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            this.execute(parameter);
        }
    }
}
