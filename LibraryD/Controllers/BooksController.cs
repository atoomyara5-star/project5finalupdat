using LibraryD.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryD.Controllers
{
    public class BooksController : Controller
    {
        private readonly MyDbContext _context;

        public BooksController(MyDbContext context)
        {
            _context = context;
        }

        // =====================================
        // Show Books (6 per page + Search)
        // =====================================
        public IActionResult Index(string? search, int page = 1)
        {
            int pageSize = 6;

            var booksQuery = _context.Books
                .Include(b => b.Status)
                .Include(b => b.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                booksQuery = booksQuery
                    .Where(b => b.BookName.Contains(search));
            }

            int totalBooks = booksQuery.Count();

            var books = booksQuery
                .OrderBy(b => b.BookId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalBooks / pageSize);
            ViewBag.Search = search;

            return View(books);
        }

        // =====================================
        // Borrow Book (عرض نموذج اختيار المدة)
        // =====================================
        [HttpGet]
        public IActionResult Borrow(int bookId)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var book = _context.Books
                .Include(b => b.Category)
                .Include(b => b.Status)
                .FirstOrDefault(b => b.BookId == bookId);

            if (book == null || book.StatusId != 1)
            {
                TempData["Error"] = "Book not available";
                return RedirectToAction("Index");
            }

            return View(book);
        }

        // =====================================
        // Borrow Book (معالجة طلب الاستعارة مع المدة)
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Borrow(int bookId, int durationDays)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var book = _context.Books.FirstOrDefault(b => b.BookId == bookId);

            if (book == null || book.StatusId != 1)
            {
                TempData["Error"] = "Book not available";
                return RedirectToAction("Index");
            }

            // التحقق من المدة (بين 1 و 30 يوم)
            if (durationDays < 1 || durationDays > 30)
            {
                TempData["Error"] = "Borrow duration must be between 1 and 30 days";
                return RedirectToAction("Index");
            }

            // منع تكرار الطلب
            bool alreadyRequested = _context.Borrowings.Any(b =>
                b.BookId == bookId &&
                b.UserId == userId &&
                (b.StatusId == 2 || b.StatusId == 3));

            if (alreadyRequested)
            {
                TempData["Error"] = "You already requested this book";
                return RedirectToAction("Index");
            }

            var borrowing = new Borrowing
            {
                BookId = bookId,
                UserId = userId.Value,
                BorrowDate = DateTime.Now,  // DateTime.Now وليس DateTime? 
                StatusId = 3, // Pending
                ReturnDate = DateTime.Now.AddDays(durationDays)
            };

            _context.Borrowings.Add(borrowing);
            _context.SaveChanges();

            TempData["Success"] = $"Borrow request sent successfully. Expected return date: {borrowing.ReturnDate.Value.ToString("yyyy-MM-dd")}";
            return RedirectToAction("Index");
        }

        // =====================================
        // Cancel Borrow Request
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelBorrow(int borrowId)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var borrow = _context.Borrowings
                .Include(b => b.Book)
                .FirstOrDefault(b => b.BorrowId == borrowId && b.UserId == userId);

            if (borrow == null)
            {
                TempData["Error"] = "Borrow request not found.";
                return RedirectToAction("MyBorrows");
            }

            if (borrow.StatusId == 3) // Pending only
            {
                _context.Borrowings.Remove(borrow);
                _context.SaveChanges();
                TempData["Success"] = "Borrow request cancelled successfully.";
            }
            else if (borrow.StatusId == 2) // Approved
            {
                TempData["Error"] = "You cannot cancel an approved borrow. Please return the book instead.";
            }
            else
            {
                TempData["Error"] = "This borrow request cannot be cancelled.";
            }

            return RedirectToAction("MyBorrows");
        }

        // =====================================
        // My Borrows (عرض طلبات الاستعارة الخاصة بالطالب)
        // =====================================
        public IActionResult MyBorrows()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var myBorrows = _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.Status)
                .Where(b => b.UserId == userId.Value)
                .OrderByDescending(b => b.BorrowDate)
                .ToList();

            return View(myBorrows);
        }

        // =====================================
        // Return Book (إعادة الكتاب)
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Return(int borrowId)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var borrow = _context.Borrowings
                .Include(b => b.Book)
                .FirstOrDefault(b =>
                    b.BorrowId == borrowId &&
                    b.UserId == userId &&
                    b.StatusId == 2); // Approved

            if (borrow != null)
            {
                borrow.StatusId = 5; // Returned
                borrow.ReturnDate = DateTime.Now;

                if (borrow.Book != null)
                {
                    borrow.Book.StatusId = 1; // Available
                }

                _context.SaveChanges();
                TempData["Success"] = "Book returned successfully";
            }
            else
            {
                TempData["Error"] = "Return failed";
            }

            return RedirectToAction("MyBorrows");
        }
    }
}