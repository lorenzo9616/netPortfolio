'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { clearToken } from '@/lib/auth';

interface NavItem {
  label: string;
  href: string;
}

const NAV_ITEMS: NavItem[] = [
  { label: 'Properties', href: '/properties' },
  { label: 'History',    href: '/history' },
  { label: 'Upload',     href: '/upload' },
];

export default function Sidebar() {
  const pathname = usePathname();
  const router   = useRouter();

  function handleSignOut() {
    clearToken();
    router.push('/login');
    router.refresh();
  }

  return (
    <aside className="flex h-screen w-64 flex-col bg-gray-900 text-white">
      <div className="flex h-16 items-center border-b border-gray-700 px-6">
        <span className="text-xl font-bold tracking-tight text-white">OpenOCR</span>
      </div>

      <nav className="flex-1 overflow-y-auto px-3 py-4">
        <ul className="space-y-1">
          {NAV_ITEMS.map((item) => {
            const isActive =
              pathname === item.href || pathname.startsWith(item.href + '/');
            return (
              <li key={item.href}>
                <Link
                  href={item.href}
                  className={`flex items-center rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
                    isActive
                      ? 'bg-blue-600 text-white'
                      : 'text-gray-300 hover:bg-gray-800 hover:text-white'
                  }`}
                >
                  {item.label}
                </Link>
              </li>
            );
          })}
        </ul>
      </nav>

      <div className="border-t border-gray-700 px-3 py-4">
        <button
          type="button"
          onClick={handleSignOut}
          className="w-full rounded-lg px-3 py-2 text-left text-sm font-medium text-gray-400 transition-colors hover:bg-gray-800 hover:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          Sign out
        </button>
      </div>
    </aside>
  );
}
