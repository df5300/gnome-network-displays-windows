/* main.c
 *
 * Copyright 2018 Benjamin Berg
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

#include <adwaita.h>
#include <glib/gi18n.h>
#include <gst/gst.h>
#include <stdlib.h>
#include "gnome-network-displays-config.h"
#include "nd-window.h"

/* Global variable to store target IP from command line */
static gchar *target_ip = NULL;

static GOptionEntry entries[] = {
  { "connect-ip", 'c', 0, G_OPTION_ARG_STRING, &target_ip, "Connect to device by IP address", "IP" },
  { NULL }
};

static void
on_activate (AdwApplication *app)
{
  GtkWindow *window;

  g_assert (GTK_IS_APPLICATION (app));

  window = g_object_new (ND_TYPE_WINDOW,
                         "application", app,
                         NULL);

  /* Set environment variable for IP connection if provided */
  if (target_ip != NULL)
    {
      g_setenv ("NETWORK_DISPLAYS_TARGET_IP", target_ip, TRUE);
      g_message ("Will attempt to connect to IP: %s", target_ip);
    }

  gtk_window_present (window);
}

int
main (int   argc,
      char *argv[])
{
  g_autoptr(AdwApplication) app = NULL;
  g_autoptr(GError) error = NULL;
  GOptionContext *context;

  /* Set up gettext translations */
  bindtextdomain (GETTEXT_PACKAGE, LOCALEDIR);
  bind_textdomain_codeset (GETTEXT_PACKAGE, "UTF-8");
  textdomain (GETTEXT_PACKAGE);

  /* Parse command line options */
  context = g_option_context_new ("- GNOME Network Displays");
  g_option_context_add_main_entries (context, entries, GETTEXT_PACKAGE);
  if (!g_option_context_parse (context, &argc, &argv, &error))
    {
      g_printerr ("Error parsing options: %s\n", error->message);
      return 1;
    }
  g_option_context_free (context);

  gst_init (&argc, &argv);

#if GLIB_CHECK_VERSION (2, 74, 0)
  app = adw_application_new ("org.gnome.NetworkDisplays", G_APPLICATION_DEFAULT_FLAGS);
#else
  app = adw_application_new ("org.gnome.NetworkDisplays", G_APPLICATION_FLAGS_NONE);
#endif

  g_set_application_name (_("GNOME Network Displays"));

  g_signal_connect (app, "activate", G_CALLBACK (on_activate), NULL);

  return g_application_run (G_APPLICATION (app), argc, argv);
}
