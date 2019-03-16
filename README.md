# Enfesto BPN
**Enfesto BPN** - проект для упрощённой реализации системы обмена данными между компьютерами. Представлен в виде **C#** библиотеки

Основной проект *(сама библиотека)* находится в папке **Enfesto BPN Library**. Там же по пути **Enfesto BPN Library/bin/Release** *(или **Enfesto BPN Library/bin/Debug**)* вы можете найти собранный вариант библиотеки

## Работа с библиотекой
Весь функционал библиотеки расположен в пространстве имён **BPN**. В настоящий момент в библиотеке имеется **3** объекта:

1. **BPNClient** - клиент для базовой реализации системы общения
2. **BPNTunnel** - система реализации туннельного соединения
3. **BPNPool** - система реализации хранилища соединений

А теперь по-порядку

### BPNClient

Метод | Аргументы | Вывод | Описание
------|-----------|-------|---------
***[конструктор]*** | | | Создание объекта клиента
| | string **name**, *[int **port** = **42569**]*
**Listen** | *[bool **infinity** = **false**]* | | Прослушивание соединений
**Send** | string **ip**, string **data** | **bool** - статус отправки данных | Отправка данных другому клиенту
| | string **ip**, int **port**, string **data**
**Push** | string **ip**, string **data** | | Бесконечные попытки отправить данные другому клиенту
| | string **ip**, int **port**, string **data**
**Get** | | **List<Dictionary<string, string>>** - список ассоциативных массивов полученных данных | Получение списка всех полученных текущим клиентом данных
| | string **name** | | Получение списка всех полученных текущим клиентом данных от пользователя с именем **name**
**GetBy** | string **name**, string **value** | **List<Dictionary<string, string>>** - список ассоциативных массивов, подходящих под описание | Получение списка всех полученных текущим клиентом данных, поднодящих под описание item[**name**] = **value**
**SetDataRecieveHandler** | Action<Dictionary<string, string>> **handler** | | Установка анонимной функции-обработчика получаемых клиентом данных
**SetFlag** | string **flag** | | Установка флага - дополнительной информации в заголовок пакета

Пример использования *(консольный вариант клиент-серверного общения)*:

Клиент
```cs
using BPN;

BPNClient Client = new BPNClient ("SuperUser");

Console.Write ("Enter IP: ");
string IP = Console.ReadLine ();

while (true)
{
    Console.Write ("Enter message: ");
    string Message = Console.ReadLine ();

    Console.WriteLine ("Message " + (Client.Send (IP, Message) ? "successfully sended" : "not sended :("));
}
```

Сервер
```cs
using BPN;

BPNClient Client = new BPNClient ("SuperUser");

Client.SetDataRecieveHandler ((data) => {
    Console.WriteLine ("Recieved from \"" + data["name"] + "\" (" + data["ip"] + "): " + data["data"]);
});

Client.Listen (true);
```

### BPNTunnel

Туннель предоставляет возможность отправки данных другому пользователю через цепочку... других пользователей. Так, к примеру, можно частично скрыть оригинального отправителя сообщения или отправить сообщения клиенту, к которому нет прямого доступа *(по примеру **VPN**; для этого лучше использовать мост, см. ниже)*

Метод | Аргументы | Вывод | Описание
------|-----------|-------|---------
***[конструктор]*** | params string[] **ips** | | Создание объекта туннеля
**SetName** | string **name** | | Установка имени отправителя данных
**SetBridge** | string **ip** | | Установка моста - участника цепочки, который всегда будет в ней на первом месте
**Send** | string **ip**, string **data** | **bool** - статус отправки данных первому участнику туннеля | Отправка данных по туннелю
| | string **ip**, int **port**, string **data**
**Push** | string **ip**, string **data** | | Бесконечные попытки отправить данные первому участнику туннеля
| | string **ip**, int **port**, string **data**

Пример использования *(отправка данных через мост - другого клиента)*:

```cs
using BPN;

Console.Write ("Enter bridge IP: ");

BPNTunnel Tunnel = new BPNTunnel ();
Tunnel.SetBridge (Console.ReadLine ());

Console.Write ("Enter IP: ");
string IP = Console.ReadLine ();

Console.Write ("Enter message: ");
string Message = Console.ReadLine ();

Tunnel.Send (IP, Message);
```

> В конструкторе туннеля можно указать список **IP** адресов. Если параметр **RandomOrder** будет выставлен как **true** *(по умолчанию)*, то этот список будет случайно перемешан. Сообщение будет отправлено поочерёдно через каждого участника туннеля

### BPNPool

Бассейн - это объект, предназначенный для хранения списка **IP** адресов клиентов и быстрой работой с ними. Он может упростить жизнь при разработке таких программ, как чатов

Метод | Аргументы | Вывод | Описание
------|-----------|-------|---------
***[конструктор]*** | params string[] **ips** | | Создание объекта бассейна
| | string **name**, params string[] **ips**
| | string **name**, int **port**, params string[] **ips**
**Send** | int **poolId**, string **data** | **bool** - статус отправки данных | Отправка данных участнику бассейна с индексом **poolId**
**Push** | int **poolId**, string **data** | | Бесконечные попытки отправить данные участнику бассейна с индексом **poolId**
**Broadcast** | string **data**, *[bool **push** = **false**]* | | Отправка данных всем участникам бассейна

Пример использования *(спам всем участникам бассейна пока они точно не получат сообщение)*:

```cs
using BPN;

BPNPool Pool = new BPNPool ("SuperUser", "1.2.3.4", "2.3.6.8", "3.7.9.1");

Pool.Broadcast ("Hello, World!", true);
```

Вот и всё. Приятного использования! :3

Автор: [Подвирный Никита](https://vk.com/technomindlp). Специально для [Enfesto Studio Group](http://vk.com/hphp_convertation)
