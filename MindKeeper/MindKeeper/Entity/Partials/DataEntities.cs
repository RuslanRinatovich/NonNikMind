using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity;

namespace MindKeeper.Entity
{
    public partial class DataEntities : DbContext
    {
        // Фабричный метод: каждый раз создаёт НОВЫЙ экземпляр контекста
        public static DataEntities GetContext()
        {
            return new DataEntities();
        }
    }
}

