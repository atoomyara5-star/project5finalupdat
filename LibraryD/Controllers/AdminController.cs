using LibraryD.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace LibraryD.Controllers
{
    public class AdminController : Controller
    {
        private readonly MyDbContext _context;

        public AdminController(MyDbContext context)
        {
            _context = context;
        }

        // ===============================
        // Dashboard
        // ===============================
        public IActionResult Dashboard()
        {
            ViewBag.UsersCount = _context.Users.Count();
            ViewBag.BooksCount = _context.Books.Count();
            ViewBag.RoomsCount = _context.Rooms.Count();
            ViewBag.PendingBorrows = _context.Borrowings.Count(b => b.StatusId == 3); // Pending
            ViewBag.PendingReservations = _context.RoomReservations.Count(r => r.StatusId == 3); // Pending
            ViewBag.OverdueBooks = _context.Borrowings.Count(b => b.StatusId == 2 && b.ReturnDate < DateTime.Now); // Overdue

            return View();
        }

        // ===============================
        // Manage Borrows
        // ===============================
        public IActionResult ManageBorrows()
        {
            var borrows = _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.User)
                .Include(b => b.Status)
                .OrderByDescending(b => b.BorrowDate)
                .ToList();

            return View(borrows);
        }

        [HttpPost]
        public IActionResult ApproveBorrow(int id)
        {
            var borrow = _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.User)
                .FirstOrDefault(b => b.BorrowId == id);

            if (borrow != null)
            {
                borrow.StatusId = 2; // Borrowed (Approved)
                if (borrow.Book != null)
                    borrow.Book.StatusId = 2; // Borrowed

                // الحفاظ على تاريخ الإرجاع الذي اختاره الطالب (إذا لم يكن محدداً، نضع 14 يوم)
                if (!borrow.ReturnDate.HasValue)
                {
                    borrow.ReturnDate = DateTime.Now.AddDays(14);
                }

                _context.Notifications.Add(new Notification
                {
                    UserId = borrow.UserId,
                    Message = $"Your borrow request for '{borrow.Book.BookName}' has been approved. Please return by {borrow.ReturnDate:yyyy-MM-dd}.",
                    IsRead = false,
                    Date = DateTime.Now
                });

                _context.SaveChanges();
                TempData["Success"] = "Borrow request approved successfully.";
            }

            return RedirectToAction("ManageBorrows");
        }

        [HttpPost]
        public IActionResult RejectBorrow(int id)
        {
            var borrow = _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.User)
                .FirstOrDefault(b => b.BorrowId == id);

            if (borrow != null)
            {
                _context.Borrowings.Remove(borrow);

                _context.Notifications.Add(new Notification
                {
                    UserId = borrow.UserId,
                    Message = $"Your borrow request for '{borrow.Book.BookName}' has been rejected.",
                    IsRead = false,
                    Date = DateTime.Now
                });

                _context.SaveChanges();
                TempData["Success"] = "Borrow request rejected successfully.";
            }

            return RedirectToAction("ManageBorrows");
        }

        [HttpPost]
        public IActionResult MarkAsReturned(int id)
        {
            var borrow = _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.User)
                .FirstOrDefault(b => b.BorrowId == id);

            if (borrow != null)
            {
                borrow.StatusId = 5; // Returned
                if (borrow.Book != null)
                    borrow.Book.StatusId = 1; // Available

                _context.Notifications.Add(new Notification
                {
                    UserId = borrow.UserId,
                    Message = $"Your borrowed book '{borrow.Book.BookName}' has been marked as returned. Thank you!",
                    IsRead = false,
                    Date = DateTime.Now
                });

                _context.SaveChanges();
                TempData["Success"] = "Book marked as returned successfully.";
            }

            return RedirectToAction("ManageBorrows");
        }

        // ===============================
        // Manage Room Reservations
        // ===============================
        public IActionResult ManageReservations()
        {
            var reservations = _context.RoomReservations
                .Include(r => r.Room)
                .Include(r => r.User)
                .Include(r => r.Status)
                .OrderByDescending(r => r.ReservationDate)
                .ToList();

            ViewBag.TotalRooms = _context.Rooms.Count();
            return View(reservations);
        }

        [HttpPost]
        public IActionResult ApproveReservation(int id)
        {
            try
            {
                var reservation = _context.RoomReservations
                    .Include(r => r.User)
                    .Include(r => r.Room)
                    .FirstOrDefault(r => r.ReservationId == id);

                if (reservation == null)
                {
                    TempData["Error"] = "Reservation not found.";
                    return RedirectToAction("ManageReservations");
                }

                reservation.StatusId = 4; // Confirmed

                _context.Notifications.Add(new Notification
                {
                    UserId = reservation.UserId,
                    Message = $"Your reservation for room '{reservation.Room?.RoomName}' on {reservation.ReservationDate:yyyy-MM-dd} at {reservation.StartTime} has been APPROVED.",
                    IsRead = false,
                    Date = DateTime.Now
                });

                _context.SaveChanges();
                TempData["Success"] = "Reservation approved successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error approving reservation: {ex.Message}";
            }

            return RedirectToAction("ManageReservations");
        }

        [HttpPost]
        public IActionResult RejectReservation(int id)
        {
            try
            {
                var reservation = _context.RoomReservations
                    .Include(r => r.User)
                    .Include(r => r.Room)
                    .FirstOrDefault(r => r.ReservationId == id);

                if (reservation == null)
                {
                    TempData["Error"] = "Reservation not found.";
                    return RedirectToAction("ManageReservations");
                }

                _context.RoomReservations.Remove(reservation);

                _context.Notifications.Add(new Notification
                {
                    UserId = reservation.UserId,
                    Message = $"Your reservation for room '{reservation.Room?.RoomName}' on {reservation.ReservationDate:yyyy-MM-dd} at {reservation.StartTime} has been REJECTED.",
                    IsRead = false,
                    Date = DateTime.Now
                });

                _context.SaveChanges();
                TempData["Success"] = "Reservation rejected successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error rejecting reservation: {ex.Message}";
            }

            return RedirectToAction("ManageReservations");
        }

        // ===============================
        // Users Management
        // ===============================
        public IActionResult UsersList()
        {
            var users = _context.Users.ToList();
            return View(users);
        }

        [HttpPost]
        public IActionResult DeleteUser(int id)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == id);
            if (user != null && user.Role != "Admin")
            {
                _context.Users.Remove(user);
                _context.SaveChanges();
                TempData["Success"] = "User deleted successfully.";
            }
            return RedirectToAction("UsersList");
        }

        // ===============================
        // Books Management
        // ===============================
        public IActionResult BooksList()
        {
            var books = _context.Books
                .Include(b => b.Category)
                .Include(b => b.Status)
                .ToList();
            return View(books);
        }

        public IActionResult AddBook()
        {
            ViewBag.Categories = _context.Category
                .Select(c => new SelectListItem
                {
                    Value = c.CategoryId.ToString(),
                    Text = c.CategoryName
                })
                .ToList();

            ViewBag.Statuses = _context.Status
                .Select(s => new SelectListItem
                {
                    Value = s.StatusId.ToString(),
                    Text = s.StatusName
                })
                .ToList();

            return View();
        }

        [HttpPost]
        public IActionResult AddBook(Book book)
        {
            if (ModelState.IsValid)
            {
                book.StatusId = 1; // Available
                _context.Books.Add(book);
                _context.SaveChanges();
                TempData["Success"] = "Book added successfully.";
                return RedirectToAction("BooksList");
            }

            ViewBag.Categories = _context.Category
                .Select(c => new SelectListItem
                {
                    Value = c.CategoryId.ToString(),
                    Text = c.CategoryName
                })
                .ToList();

            ViewBag.Statuses = _context.Status
                .Select(s => new SelectListItem
                {
                    Value = s.StatusId.ToString(),
                    Text = s.StatusName
                })
                .ToList();

            return View(book);
        }

        public IActionResult EditBook(int id)
        {
            var book = _context.Books.FirstOrDefault(b => b.BookId == id);
            if (book == null) return NotFound();

            ViewBag.Categories = _context.Category
                .Select(c => new SelectListItem
                {
                    Value = c.CategoryId.ToString(),
                    Text = c.CategoryName
                })
                .ToList();

            ViewBag.Statuses = _context.Status
                .Select(s => new SelectListItem
                {
                    Value = s.StatusId.ToString(),
                    Text = s.StatusName
                })
                .ToList();

            return View(book);
        }

        [HttpPost]
        public IActionResult EditBook(Book book)
        {
            if (ModelState.IsValid)
            {
                _context.Books.Update(book);
                _context.SaveChanges();
                TempData["Success"] = "Book updated successfully.";
                return RedirectToAction("BooksList");
            }

            ViewBag.Categories = _context.Category
                .Select(c => new SelectListItem
                {
                    Value = c.CategoryId.ToString(),
                    Text = c.CategoryName
                })
                .ToList();

            ViewBag.Statuses = _context.Status
                .Select(s => new SelectListItem
                {
                    Value = s.StatusId.ToString(),
                    Text = s.StatusName
                })
                .ToList();

            return View(book);
        }

        [HttpPost]
        public IActionResult DeleteBook(int id)
        {
            var book = _context.Books.FirstOrDefault(b => b.BookId == id);
            if (book != null)
            {
                _context.Books.Remove(book);
                _context.SaveChanges();
                TempData["Success"] = "Book deleted successfully.";
            }
            return RedirectToAction("BooksList");
        }

        // ===============================
        // Rooms Management (إدارة الغرف)
        // ===============================

        // عرض قائمة الغرف
        public IActionResult RoomsList()
        {
            var rooms = _context.Rooms
                .Include(r => r.Status)
                .ToList();
            return View(rooms);
        }

        // عرض صفحة إضافة غرفة جديدة
        [HttpGet]
        public IActionResult AddRoom()
        {
            ViewBag.Statuses = _context.Status
                .Select(s => new SelectListItem
                {
                    Value = s.StatusId.ToString(),
                    Text = s.StatusName
                })
                .ToList();
            return View();
        }

        // إضافة غرفة جديدة
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddRoom(Room room)
        {
            if (ModelState.IsValid)
            {
                // إذا لم يتم تحديد حالة، اجعلها Available
                if (room.StatusId == 0)
                    room.StatusId = 1;

                _context.Rooms.Add(room);
                _context.SaveChanges();
                TempData["Success"] = "Room added successfully.";
                return RedirectToAction("RoomsList");
            }

            ViewBag.Statuses = _context.Status
                .Select(s => new SelectListItem
                {
                    Value = s.StatusId.ToString(),
                    Text = s.StatusName
                })
                .ToList();
            return View(room);
        }

        // عرض صفحة تعديل غرفة
        [HttpGet]
        public IActionResult EditRoom(int id)
        {
            var room = _context.Rooms.FirstOrDefault(r => r.RoomId == id);
            if (room == null) return NotFound();

            ViewBag.Statuses = _context.Status
                .Select(s => new SelectListItem
                {
                    Value = s.StatusId.ToString(),
                    Text = s.StatusName
                })
                .ToList();
            return View(room);
        }

        // تعديل غرفة
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditRoom(Room room)
        {
            if (ModelState.IsValid)
            {
                _context.Rooms.Update(room);
                _context.SaveChanges();
                TempData["Success"] = "Room updated successfully.";
                return RedirectToAction("RoomsList");
            }

            ViewBag.Statuses = _context.Status
                .Select(s => new SelectListItem
                {
                    Value = s.StatusId.ToString(),
                    Text = s.StatusName
                })
                .ToList();
            return View(room);
        }

        // حذف غرفة
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteRoom(int id)
        {
            var room = _context.Rooms.FirstOrDefault(r => r.RoomId == id);
            if (room != null)
            {
                // التحقق من عدم وجود حجوزات مرتبطة بهذه الغرفة
                bool hasReservations = _context.RoomReservations.Any(r => r.RoomId == id);
                if (hasReservations)
                {
                    TempData["Error"] = "Cannot delete room with existing reservations.";
                    return RedirectToAction("RoomsList");
                }

                _context.Rooms.Remove(room);
                _context.SaveChanges();
                TempData["Success"] = "Room deleted successfully.";
            }
            return RedirectToAction("RoomsList");
        }

        // ===============================
        // Overdue Books
        // ===============================
        public IActionResult OverdueBooks()
        {
            var overdueBooks = _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.User)
                .Include(b => b.Status)
                .Where(b => b.StatusId == 2 && b.ReturnDate < DateTime.Now)
                .OrderBy(b => b.ReturnDate)
                .ToList();

            return View(overdueBooks);
        }

        [HttpPost]
        public IActionResult SendOverdueNotification(int id)
        {
            var borrow = _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.User)
                .FirstOrDefault(b => b.BorrowId == id);

            if (borrow != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = borrow.UserId,
                    Message = $"Reminder: The book '{borrow.Book.BookName}' is overdue. Please return it as soon as possible.",
                    IsRead = false,
                    Date = DateTime.Now
                });

                _context.SaveChanges();
                TempData["Success"] = "Overdue notification sent.";
            }

            return RedirectToAction("OverdueBooks");
        }

        // ===============================
        // Helper: Get status name
        // ===============================
        public string GetStatusName(int statusId)
        {
            return statusId switch
            {
                1 => "Available",
                2 => "Borrowed",
                3 => "Pending",
                4 => "Confirmed",
                5 => "Returned",
                _ => "Unknown",
            };
        }
    }
}